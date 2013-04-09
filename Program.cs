using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
namespace gallery_builder {
	class Program {
		static void Main( string[] args ) {
			string template_dir = _q("Input template dir");//Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"..\\..\\tpls");
			string images_big = _q("Input images path");
			string images_small = _q("Preview gen images path");
			string output_dir = _q("Output dir");
			int imgs_per_page = int.Parse(_q("Images per page"));
			int max_imgs = int.Parse(_q("Max imgs"));
			var _tmp_sz = _q("Max preview size(WxH)").Split(new char[] { 'x' }).Select(a => int.Parse(a)).ToArray();
			Size img_size = new Size(_tmp_sz[0], _tmp_sz[1]);

			var gen = new Generator() {
				TplDir = template_dir,
				ImgDir = images_big,
				PreviewDir = images_small,
				OutputDir = output_dir,
				ImagesPerPage = imgs_per_page,
				PreviewSize = img_size,
				Files = Directory.EnumerateFiles(images_big).Take(max_imgs).ToArray()
			};
			Console.WriteLine("Generating previews");
			gen.ResizeImages();
			Console.WriteLine("Generating pages");
			gen.Generate();
		}
		static string _q( string s ) {
			Console.Write(s + Environment.NewLine + '>');
			return Console.ReadLine();
		}
	}
	class Generator {
		public string PreviewDir, ImgDir, OutputDir, TplDir, PreviewPrefix = "s_";
		public string[] Files;
		public int ImagesPerPage;
		public Size PreviewSize;

		public void Generate() {
			//paginate
			string[][] _pages = Enumerable.
				Range(0, Files.Length).
				GroupBy(a => a / ImagesPerPage).
				Select(a => a.Select(b => Files[b]).ToArray()).
				ToArray();
			//load templates
			string page_tpl = File.ReadAllText(Path.Combine(TplDir, "main.tpl"));
			string entity_tpl = File.ReadAllText(Path.Combine(TplDir, "entity.tpl"));
			//generate
			for ( int i = 0; i < _pages.Length; i++ ) {
				File.WriteAllText(Path.Combine(OutputDir, ( i == 0 ? "index" : i.ToString() ) + ".html"),
						CreatePage(_pages[i], page_tpl, entity_tpl, i)
					);
			}
			//1-lvl copy
			//4 css, scripts, etc
			foreach ( string directory in Directory.GetDirectories(TplDir) ) {
				string target_dir = Path.Combine(OutputDir, Path.GetFileName(directory));
				if ( !Directory.Exists(target_dir) ) {
					Directory.CreateDirectory(target_dir);
				}
				foreach ( string file in Directory.GetFiles(directory) ) {
					string target_file = Path.Combine(target_dir, Path.GetFileName(file));
					if ( !File.Exists(target_file) )
						File.Copy(file, target_file);
				}
			}
		}
		public void ResizeImages() {
			string[] _ReadyFiles = Directory.GetFiles(PreviewDir).Select(Path.GetFileName).ToArray();
			Array.Sort(_ReadyFiles);
			//only !converted
			string[] _ConvertFiles = null;
			if ( _ReadyFiles.Length > 0 ) {
				_ConvertFiles = this.Files.
				Where(a => Array.BinarySearch(_ReadyFiles, PreviewPrefix + Path.GetFileName(a)) < 0).
				ToArray();
			}
			else {
				_ConvertFiles = this.Files;
			}
			Parallel.ForEach(_ConvertFiles,
#if DEBUG
				new ParallelOptions() {
				MaxDegreeOfParallelism=1
			},
#endif
				a => {
				Bitmap b = (Bitmap)Bitmap.FromFile(a);
				//prepare width, height
				var _scale = Math.Min((double)PreviewSize.Width / b.Width, (double)PreviewSize.Height / b.Height);
				var _width = (int)( b.Width * _scale );
				var _height = (int)( b.Height * _scale );
				Bitmap _output = new Bitmap(_width, _height);
				Graphics _g = Graphics.FromImage(_output);
				//resize
				_g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				_g.DrawImage(b, 0, 0, _width, _height);
				_g.Flush();
				//save
				_output.Save(Path.Combine(PreviewDir, PreviewPrefix + Path.GetFileName(a)));
				//gc
				_output.Dispose();
				b.Dispose();
				_g.Dispose();
			});
		}
		public string CreatePage( string[] _pages, string _page_tpl, string _entity_tpl, int _index ) {
			//determine if last o first
			int right = _index < Files.Length / ImagesPerPage ? _index + 1 : -1;
			int left = _index > 1 ? _index - 1 : -1;
			string _PreviewDir = Path.GetFileName(this.PreviewDir);
			string _ImgDir = Path.GetFileName(this.ImgDir);

			return String.Format(
				//template
										_page_tpl,
				//name
										( _index != 0 ? _index.ToString() : "index" ),
				//gallery
										String.Concat(
											_pages.
												Select(Path.GetFileName).
												Select(a =>
													String.Format(
														_entity_tpl,
														Path.Combine(
															_PreviewDir,
															PreviewPrefix + a),
														Path.Combine(
															_ImgDir,
															a
														)
												)
											)
										),
				//link to left
										( left != -1 ? left.ToString() : "index" ) + ".html",
				//link left text
										_index != 0 ? "←" : "",
				//same 4 right
										( right != -1 ? right.ToString() : "index" ) + ".html",
										right != -1 ? "→" : ""
									);
		}

	}
}
