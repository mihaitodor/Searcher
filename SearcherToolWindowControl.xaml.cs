namespace Searcher
{
    using Microsoft.VisualStudio.Shell;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Media;

    /// <summary>
    /// Interaction logic for SearcherToolWindowControl.
    /// </summary>
    public partial class SearcherToolWindowControl : UserControl
    {
        private readonly EnvDTE.DTE dte = (EnvDTE.DTE)Package.GetGlobalService(typeof(EnvDTE.DTE));

        Process searcher;
        Stopwatch stopwatch = new Stopwatch();

        private struct SearchTool
        {
            public string Name { get; set; }
            public string Executable { get; set; }
            public string DefaultArgs { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }
        private List<SearchTool> searchTools = new List<SearchTool>()
        {
            new SearchTool() { Name = "ripgrep", Executable = "rg.exe", DefaultArgs = "-n -g \"!Libraries/**\" --type cpp" },
            new SearchTool() { Name = "The Silver Searcher", Executable = "ag.exe", DefaultArgs = "--ignore \"./Libraries/\" --cpp" },
            new SearchTool() { Name = "The Platinum Searcher", Executable = "pt.exe", DefaultArgs = "" },
            new SearchTool() { Name = "grep", Executable = @"C:\Program Files\Git\usr\bin\grep.exe", DefaultArgs = "--exclude-dir=\"Libraries\" --include=\"*.cpp\" --include=\"*.h\" -rnw . -e" },
        };

        public SearcherToolWindowControl()
        {
            this.InitializeComponent();
        }

        private void browseLocationButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    locationTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void InitSearher()
        {
            if (searcher != null)
            {
                searcher.Close();
            }

            searcher = new Process();

            searcher.StartInfo.UseShellExecute = false;
            searcher.StartInfo.CreateNoWindow = true;
            searcher.StartInfo.RedirectStandardOutput = true;
            searcher.StartInfo.RedirectStandardError = true;
            searcher.EnableRaisingEvents = true;

            // Searcher process async handlers
            searcher.OutputDataReceived += new DataReceivedEventHandler(SearcherOutputHandler);
            searcher.ErrorDataReceived += new DataReceivedEventHandler(SearcherOutputHandler);
            searcher.Exited += new EventHandler(SearcherExited);
        }

        private void searcherWindow_Loaded(object sender, RoutedEventArgs e)
        {
            searchToolsComboBox.ItemsSource = searchTools;

            InitSearher();
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            if (searchButton.Content.ToString() == "Stop")
            {
                searcher.Kill();
                searcher.Close();
                searchButton.IsEnabled = false;
                return;
            }

            resultsStackPanel.Children.Clear();
            elapsedTextBox.Text = "";

            InitSearher();
            searcher.StartInfo.FileName = ((SearchTool)searchToolsComboBox.SelectedItem).Executable;
            searcher.StartInfo.Arguments = argsTextBox.Text + " " + searchTermTextBox.Text;
            searcher.StartInfo.WorkingDirectory = locationTextBox.Text;

            //Start process and handlers
            searchButton.Content = "Stop";

            stopwatch.Reset();
            stopwatch.Start();
            try
            {
                searcher.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            searcher.BeginOutputReadLine();
            searcher.BeginErrorReadLine();
        }

        private TextBox StringToTextBox(string text, Color? color = null)
        {
            var tb = new TextBox() {
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(color ?? Colors.Black),
                Text = text
            };

            tb.MouseDoubleClick += resultTextBox_DoubleClick;

            return tb;
        }

        private void resultTextBox_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var result = ((TextBox)sender).Text;

            var elements = result.Split(':');

            // We're looking for file:line_no:match
            if (elements.Length > 2)
            {
                // Some tools return Unix paths
                string file = Path.Combine(locationTextBox.Text, elements[0].Replace('/', '\\'));

                if (!File.Exists(file))
                {
                    return;
                }

                int lineNo = 0;
                if (!Int32.TryParse(elements[1], out lineNo))
                {
                    return;
                }

                EnvDTE.Window window = dte.ItemOperations.OpenFile(file, EnvDTE.Constants.vsViewKindCode);
                window.Activate();
                window.SetFocus();

                ((EnvDTE.TextSelection)dte.ActiveDocument.Selection).GotoLine(lineNo, true);
            }
        }

        private void SearcherOutputHandler(object sendingProcess, DataReceivedEventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                if (eventArgs.Data != null && eventArgs.Data.ToString() != string.Empty)
                {
                    var result = eventArgs.Data.ToString();
                    resultsStackPanel.Children.Add(StringToTextBox(result));
                }
            });
        }

        void SearcherExited(object sender, System.EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                searcher.Close();
                stopwatch.Stop();
                elapsedTextBox.Text = stopwatch.Elapsed.ToString();

                searchButton.Content = "Search";
                searchButton.IsEnabled = true;
            });
        }

        private void searchersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EnableDisableSearch(null, null);
            
            argsTextBox.Text = ((SearchTool)searchToolsComboBox.SelectedItem).DefaultArgs;
        }

        private void EnableDisableSearch(object sender, TextChangedEventArgs e)
        {
            if (locationTextBox.Text != string.Empty &&
                searchToolsComboBox.SelectedIndex >= 0 &&
                searchTermTextBox.Text != string.Empty)
            {
                searchButton.IsEnabled = true;
            }
            else
            {
                searchButton.IsEnabled = false;
            }
        }

        
    }
}