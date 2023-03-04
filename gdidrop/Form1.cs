using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CueSharp;
using gdidrop.Properties;

namespace gdidrop
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            images = new Image[]
            {
                Resources.working_1,
                Resources.working_2,
                Resources.working_1,
                Resources.working_3
            };
            idleImage = Resources.idle;
        }

        private Image idleImage;
        private Image[] images;
        private int animationFrame;
        private bool inProgress;
        private Thread convertThread;

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;

            object insert = e.Data.GetData(DataFormats.FileDrop);
            string[] filenames = (string[]) insert;
            if (filenames.Length != 1)
                return;
            string filename = filenames[0];
            if (!File.Exists(filename))
                return;
            string extension = Path.GetExtension(filename.ToLowerInvariant());
            if (!extension.Equals(".cue"))
                return;
            e.Effect = DragDropEffects.Copy;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            object insert = e.Data.GetData(DataFormats.FileDrop);
            string[] filenames = (string[])insert;
            string filename = filenames[0];

            progressBar1.Maximum = 99;
            progressBar1.Value = 0;
            animationFrame = 0;
            inProgress = true;

            ThreadDto threadData = new ThreadDto();
            threadData.cueFileInfo = new FileInfo(filename);

            convertThread = new Thread(DoConversion);
            convertThread.Priority = ThreadPriority.Lowest;
            convertThread.Name = "Converter";
            convertThread.Start(threadData);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (inProgress)
            {
                pictureBox1.BackgroundImage = images[animationFrame++ % images.Length];
            }
            else
            {
                if (animationFrame != 0)
                {
                    pictureBox1.BackgroundImage = idleImage;
                    animationFrame = 0;
                }
            }
        }

        class ThreadDto
        {
            public FileInfo cueFileInfo;
        }

        private void DoConversion(object threadDto)
        {
            ThreadDto dto = (ThreadDto) threadDto;
            DirectoryInfo workingDirectory = dto.cueFileInfo.Directory;
            CueSheet cueSheet = new CueSheet(dto.cueFileInfo.FullName);
            int currentSector = 0;
            StringWriter gdiOutput = new StringWriter();

            Invoke((MethodInvoker) delegate { progressBar1.Maximum = cueSheet.Tracks.Length; });
            gdiOutput.WriteLine(cueSheet.Tracks.Length.ToString());
            for (int i = 0; i < cueSheet.Tracks.Length; i++)
            {
                Track currentTrack = cueSheet.Tracks[i];
                string inputTrackFilePath = Path.Combine(workingDirectory.FullName, currentTrack.DataFile.Filename);
                bool canPerformFullCopy = currentTrack.Indices.Length == 1;
                string outputTrackFileName = string.Format(
                    "track{0}.{1}",
                    currentTrack.TrackNumber,
                    currentTrack.TrackDataType == DataType.AUDIO ? "raw" : "bin");
                string outputTrackFilePath = Path.Combine(workingDirectory.FullName, outputTrackFileName);
                int sectorAmount;
                if (canPerformFullCopy)
                {
                    File.Copy(inputTrackFilePath, outputTrackFilePath);
                    sectorAmount = (int)(new FileInfo(inputTrackFilePath).Length / 2352);
                }
                else
                {
                    int gapOffset = CountIndexFrames(currentTrack.Indices[1]);
                    sectorAmount = CopyFileWithGapOffset(inputTrackFilePath, outputTrackFilePath, gapOffset, currentTrack.TrackNumber, cueSheet);
                    currentSector += gapOffset;
                }

                int gap = 0;

                gdiOutput.WriteLine("{0} {1} {2} 2352 {3} {4}", 
                    currentTrack.TrackNumber, 
                    currentSector,
                    currentTrack.TrackDataType == DataType.AUDIO ? "0" : "4",
                    outputTrackFileName,
                    gap);

                Invoke((MethodInvoker)delegate { progressBar1.Value++; });
                currentSector += sectorAmount;

                if (currentTrack.Comments.Contains("HIGH-DENSITY AREA"))
                    if (currentSector < 45000)
                        currentSector = 45000;
            }
            
            string gdiOutputPath = Path.Combine(workingDirectory.FullName, "disc.gdi");
            File.WriteAllText(gdiOutputPath, gdiOutput.ToString());
            inProgress = false;
        }

        private int CountIndexFrames(Index index)
        {
            int result = index.Frames;
            result += (index.Seconds * 75);
            result += ((index.Minutes * 60) * 75);
            return result;
        }

        private int CopyFileWithGapOffset(string inputFile, string outputFile, int frames, int trackNum, CueSheet cue)
        {
            Stream infile = File.OpenRead(inputFile);
            Stream outfile = File.OpenWrite(outputFile);
            int blockSize = 2352;
            infile.Position = frames * blockSize;
            int result = (int)((infile.Length - infile.Position) / blockSize);
            byte[] buffer = new byte[blockSize];
            while (blockSize > 0)
            {
                blockSize = infile.Read(buffer, 0, blockSize);
                outfile.Write(buffer, 0, blockSize);
            }
            if(frames%2 == 0 && trackNum != 2 && trackNum + 1 != cue.Tracks.Length)
            {
                Array.Clear(buffer, 0, buffer.Length);
                blockSize = 2352;
                while(frames > 0)
                {
                    outfile.Write(buffer, 0, blockSize);
                    frames--;
                }
            }
            if (trackNum + 1 == cue.Tracks.Length)
            {
                frames = 75;
                Array.Clear(buffer, 0, buffer.Length);
                blockSize = 2352;
                while (frames > 0)
                {
                    outfile.Write(buffer, 0, blockSize);
                    frames--;
                }
            }
            outfile.Flush();
            outfile.Close();
            infile.Close();
            return result;
        }

        private bool mouseDown;
        private Point lastLocation;
        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            lastLocation = e.Location;
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                this.Location = new Point(
                    (this.Location.X - lastLocation.X) + e.X, (this.Location.Y - lastLocation.Y) + e.Y);

                this.Update();
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }
    }
}
