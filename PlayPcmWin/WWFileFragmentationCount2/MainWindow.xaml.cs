using System.Text;
using System.Windows;
using System;

namespace WWFileFragmentationCount2 {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            //Console.WriteLine("Console Window for WWFileFragmentationCount2.");
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            var ffc = new WWFileFragmentationCount();

            string filePath = textBoxFileName.Text;
            
            var r = ffc.Run(filePath);
            if (r == null) {
                textBoxResult.Text = string.Format("Failed to read {0} !", filePath);
            } else {
                // 成功。

                string driveLetter = filePath.Substring(0, 2);

                var sb = new StringBuilder();

                sb.AppendFormat("Volume {0} , sector size={1} bytes, cluster size={2} bytes.\n", driveLetter, r.bytesPerSector, r.bytesPerCluster);
                sb.AppendFormat("File name: {0}\n", filePath);
                sb.AppendFormat("File data total cluster count: {0}\n", r.nClusters);

                if (r.nFragmentCount <= 1) {
                    sb.AppendFormat("File data is not fragmented. It is stored contiguously from logical cluster number {0} to {1} of the volume {2} .\n",
                        r.lcnVcn[0].startLcn, r.lcnVcn[0].startLcn + r.nClusters - 1, driveLetter);
                } else {
                    sb.AppendFormat("File data is fragmented. Total {0} fragments.\n", r.nFragmentCount);

                    if (WWFileFragmentationCount.TRUNCATE_FRAGMENT_NUM < r.nFragmentCount) {
                        sb.AppendFormat("The first {0} fragments are shown.\n",
                            WWFileFragmentationCount.TRUNCATE_FRAGMENT_NUM);
                    }

                    long startVcn = r.startVcn;
                    for (int i = 0; i < r.lcnVcn.Length; ++i) {
                        var l= r.lcnVcn[i];
                        var cn = l.nextVcn - startVcn;
                        if (cn == 1) {
                            sb.AppendFormat("  Fragment#{0}: VCN {1}, size={5} bytes, is stored on Logical cluster number {3} of the volume {6}\n",
                                i, startVcn, l.nextVcn - 1, l.startLcn, l.startLcn + cn - 1, cn * r.bytesPerCluster, driveLetter);
                        } else {
                            sb.AppendFormat("  Fragment#{0}: VCN {1} to {2}, size={5} bytes, is stored from Logical cluster number {3} to {4} of the volume {6}\n",
                                i, startVcn, l.nextVcn - 1, l.startLcn, l.startLcn + cn - 1, cn * r.bytesPerCluster, driveLetter);
                        }
                        startVcn = l.nextVcn;
                    }
                }

                sb.AppendFormat("File inspect finished.\n");


                textBoxResult.Text = sb.ToString();
            }
        }

        private void buttonBrowse_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            textBoxFileName.Text = dlg.FileName;
        }

        private void textBoxFileName_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effects = DragDropEffects.Copy;
            } else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void textBoxFileName_Drop(object sender, DragEventArgs e) {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (null == paths) {
                MessageBox.Show("Error: Dropped data is not file.");
                return;
            }
            textBoxFileName.Text = paths[0];
        }

        private void textBoxFileName_PreviewDragOver(object sender, DragEventArgs e) {
            e.Handled = true;
        }
    }
}
