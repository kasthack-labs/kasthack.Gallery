using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace gallery_builder {
    internal static class Program {
        private static void Main() {
            var imagesBig = _q("sourceDir");
            var previewSize = _q("imagesPerPage").Split('x').Select(int.Parse).ToArray();
            var gen = new Generator {
                TplDir = _q("tplDir"),
                ImgDir = imagesBig,
                PreviewDir = _q("thumbDir"),
                OutputDir = _q("destDir"),
                ImagesPerPage = int.Parse(_q("imagesPerPage")),
                PreviewSize = new Size(previewSize[0], previewSize[1]),
                Files = Directory.EnumerateFiles(imagesBig).Take(int.Parse(_q("maxImgs"))).ToArray()
            };
            Console.WriteLine("Generating previews");
            gen.ResizeImages();
            Console.WriteLine("Generating pages");
            gen.Generate();
        }

        public static string _q(string s) { return ConfigurationManager.AppSettings[s]; }

        public static void _e(string s) {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(s);
            Console.ForegroundColor = c;
        }
    }

    internal class Generator {
        private class Cmprr : IComparer<string> {
            public int Compare(string x, string y) { return String.CompareOrdinal(x, y); }
        }
        private const string PreviewPrefix = "s_";
        private const string Extension = "jpg";
        private readonly ImageFormat _previewImageFormat = ImageFormat.Jpeg;
        public string[] Files;
        public int ImagesPerPage;
        public string ImgDir;
        public string OutputDir;
        public string PreviewDir;
        public Size PreviewSize;
        public string TplDir;

        public void Generate() {
            //paginate
            var pages = Enumerable.Range(0, this.Files.Length)
                          .GroupBy(a => a / this.ImagesPerPage)
                          .Select(a => a.Select(b => this.Files[b]).ToArray())
                          .ToArray();
            var pageTpl = File.ReadAllText(Path.Combine(this.TplDir, "main.tpl"));
            var entityTpl = File.ReadAllText(Path.Combine(this.TplDir, "entity.tpl"));
            for (var i = 0; i < pages.Length; i++)
                File.WriteAllText(
                    Path.Combine(this.OutputDir, (i == 0 ? "index" : i.ToString(CultureInfo.InvariantCulture)) + ".html"),
                    this.CreatePage(pages[i], pageTpl, entityTpl, i));
            //1-lvl copy
            //4 css, scripts, etc
            foreach (var directory in Directory.GetDirectories(this.TplDir)) {
                DirCopy(directory, GetMoveName(directory, this.OutputDir));
            }
        }

        private static void DirCopy(string source, string dest) {
            if (!Directory.Exists(dest))
                Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source)) {
                var targetFile = GetMoveName(file, dest);
                if (!File.Exists(targetFile))
                    File.Copy(file, targetFile);
            }
            foreach (var directory in Directory.EnumerateDirectories(source))
                DirCopy(directory, GetMoveName(directory, dest));
        }

        private static string GetMoveName(string source, string destDir) {
            return Path.Combine(source, Path.GetFileName(destDir));
        }

        public void ResizeImages() {
            var readyFiles = Directory.GetFiles(this.PreviewDir).Select(Path.GetFileNameWithoutExtension).ToArray();
            const int gcFreq = 30;
            var gcCur = 0;
            Array.Sort(readyFiles);
            //only !converted
            var convertFiles =
                this.Files.Where(
                    a =>
                    Array.BinarySearch(
                        readyFiles,
                        PreviewPrefix + Path.GetFileNameWithoutExtension(a),
                        new Cmprr()
                        ) < 0).ToArray();
            //ignore bad recs
            convertFiles = convertFiles.Where(a => new FileInfo(a).Length > 2).ToArray();

            Parallel.ForEach(
                convertFiles,
                a => {
                    try {
                        using (var b = (Bitmap)Image.FromFile(a)) {
                            //prepare width, height
                            var scale = Math.Min((double)this.PreviewSize.Width / b.Width, (double)this.PreviewSize.Height / b.Height);
                            var width = (int)(b.Width * scale);
                            var height = (int)(b.Height * scale);
                            using (var output = new Bitmap(width, height, PixelFormat.Format16bppRgb555)) {
                                using (var g = Graphics.FromImage(output)) {
                                    //resize
                                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    g.DrawImage(b, 0, 0, width, height);
                                    g.Flush();
                                    //save
                                }
                                output.Save(
                                    Path.Combine(this.PreviewDir, PreviewPrefix + Path.GetFileNameWithoutExtension(a)) + '.' + Extension,
                                    this._previewImageFormat);
                            }
                        }
                        if (++gcCur % gcFreq == 0)
                            GC.Collect();
                    }
                    catch (Exception ex) {
                        Program._e(String.Format("Error processing {0}: {1}", a, ex.Message));
                    }
                });
        }

        private string CreatePage(IEnumerable<string> pages, string pageTpl, string entityTpl, int index) {
            //determine if last o first
            var right = index < this.Files.Length / this.ImagesPerPage ? index + 1 : -1;
            var left = index > 1 ? index - 1 : -1;
            var previewDir = Path.GetFileName(this.PreviewDir);
            var imgDir = Path.GetFileName(this.ImgDir);

            return String.Format(
                //template
                pageTpl,
                //name
                (index != 0 ? index.ToString() : "index"),
                //gallery
                String.Concat(
                    pages.Select(Path.GetFileName)
                         .Select(
                             a =>
                             String.Format(
                                 entityTpl,
                                 Path.Combine(previewDir, PreviewPrefix + Path.GetFileNameWithoutExtension(a) + '.' + Extension),
                                 Path.Combine(imgDir, a)))),
                //link to left
                (left != -1 ? left.ToString() : "index") + ".html",
                //link left text
                index != 0 ? "←" : "",
                //same 4 right
                (right != -1 ? right.ToString() : "index") + ".html",
                right != -1 ? "→" : "");
        }
    }

}