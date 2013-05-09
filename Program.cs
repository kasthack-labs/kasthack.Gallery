using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
namespace gallery_builder {
	class Program {
		static void Main( string[] args ) {
			#region Input & debug values
			string template_dir =
#if !DEBUG
				_q("Input template dir");
#else
 Path.Combine( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ), "..\\..\\tpls" );
#endif
			string images_big =
#if !DEBUG
				_q("Input images path");
#else
 @"e:\gc\furryporn.tk\bfl";
#endif
			string output_dir =
#if !DEBUG
				_q("Output dir");
#else
 @"E:\gc\furryporn.tk\html";
#endif
			string images_small =
#if !DEBUG
				_q("Output preview images path");
#else
 @"E:\gc\furryporn.tk\prev";
#endif
			int max_imgs =
#if !DEBUG
				int.Parse(_q("Max imgs"));
#else
 4000;
#endif
			int imgs_per_page =
#if !DEBUG
				int.Parse(_q("Images per page"));
#else
 100;
#endif
			Size img_size;
#if !DEBUG
			var _tmp_sz =_q("Max preview size(WxH)").Split(new char[] { 'x' }).Select(a => int.Parse(a)).ToArray();
			img_size = new Size(_tmp_sz[0], _tmp_sz[1]);
#else
			img_size = new Size( 180, 135 );
#endif
			#endregion
			var gen = new Generator() {
				TplDir = template_dir,
				ImgDir = images_big,
				PreviewDir = images_small,
				OutputDir = output_dir,
				ImagesPerPage = imgs_per_page,
				PreviewSize = img_size,
				Files = Directory.EnumerateFiles( images_big ).Take( max_imgs ).ToArray()
			};
			Console.WriteLine( "Generating previews" );
			gen.ResizeImages();
			Console.WriteLine( "Generating pages" );
			gen.Generate();
		}
		public static string _q( string s ) {
			Console.Write( s + Environment.NewLine + '>' );
			return Console.ReadLine();
		}
		public static void _e( string s ) {
			var c = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine( s );
			Console.ForegroundColor = c;
		}
	}
	class Generator {
		public string PreviewDir, ImgDir, OutputDir, TplDir, PreviewPrefix = "s_", Extension = "jpg";
		public string[] Files;
		public int ImagesPerPage;
		public Size PreviewSize;
		public ImageFormat PreviewImageFormat = ImageFormat.Jpeg;
		public void Generate() {
			//paginate
			string[][] _pages = Enumerable.
				Range( 0, Files.Length ).
				GroupBy( a => a / ImagesPerPage ).
				Select( a => a.Select( b => Files[ b ] ).ToArray() ).
				ToArray();
			//load templates
			string page_tpl = File.ReadAllText( Path.Combine( TplDir, "main.tpl" ) );
			string entity_tpl = File.ReadAllText( Path.Combine( TplDir, "entity.tpl" ) );
			//generate
			for ( int i = 0; i < _pages.Length; i++ ) {
				File.WriteAllText( Path.Combine( OutputDir, ( i == 0 ? "index" : i.ToString() ) + ".html" ),
						CreatePage( _pages[ i ], page_tpl, entity_tpl, i )
					);
			}
			//1-lvl copy
			//4 css, scripts, etc
			foreach ( string directory in Directory.GetDirectories( TplDir ) ) {
				string target_dir = Path.Combine( OutputDir, Path.GetFileName( directory ) );
				if ( !Directory.Exists( target_dir ) ) {
					Directory.CreateDirectory( target_dir );
				}
				foreach ( string file in Directory.GetFiles( directory ) ) {
					string target_file = Path.Combine( target_dir, Path.GetFileName( file ) );
					if ( !File.Exists( target_file ) )
						File.Copy( file, target_file );
				}
			}
		}
		public void ResizeImages() {
			string[] _ReadyFiles = Directory.GetFiles( PreviewDir ).Select( Path.GetFileNameWithoutExtension ).ToArray();
			string[] _ConvertFiles = null;
			int gc_freq = 30, gc_cur=0;
			Array.Sort( _ReadyFiles );
			//only !converted
			_ConvertFiles = this.Files.
				Where(
					a =>
							Array.
								BinarySearch(
									_ReadyFiles,
									PreviewPrefix +
									Path.GetFileNameWithoutExtension( a ),
									new CMPRR()//ordinal string comparison is ~40 times fater. N33d to process big(>50K) image dirs.
								) < 0
				).
				ToArray();
			//ignore bad recs
			_ConvertFiles = _ConvertFiles.Where( a => new FileInfo( a ).Length > 2 ).ToArray();

			Parallel.ForEach( _ConvertFiles,
				a => {
					try {
						Bitmap b = (Bitmap)Bitmap.FromFile( a );
						//prepare width, height
						var _scale = Math.Min( (double)PreviewSize.Width / b.Width, (double)PreviewSize.Height / b.Height );
						var _width = (int)( b.Width * _scale );
						var _height = (int)( b.Height * _scale );
						Bitmap _output = new Bitmap( _width, _height, PixelFormat.Format16bppRgb555 );
						Graphics _g = Graphics.FromImage( _output );
						//resize
						_g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
						_g.DrawImage( b, 0, 0, _width, _height );
						_g.Flush();
						//save
						_output.Save(
							Path.Combine(
								PreviewDir,
								PreviewPrefix +
									Path.GetFileNameWithoutExtension( a ) ) +
									'.' + this.Extension,
								this.PreviewImageFormat
						);
						//gc
						_output.Dispose();
						b.Dispose();
						_g.Dispose();
						_output = null;
						b = null;
						_g = null;
						if ( ++gc_cur % gc_freq == 0 )
							GC.Collect();
					}
					catch ( Exception ex ) {
						Program._e(String.Format("Error processing {0}: {1}",a, ex.Message ));
					}
				} );
		}
		public string CreatePage( string[] _pages, string _page_tpl, string _entity_tpl, int _index ) {
			//determine if last o first
			int right = _index < Files.Length / ImagesPerPage ? _index + 1 : -1;
			int left = _index > 1 ? _index - 1 : -1;
			string _PreviewDir = Path.GetFileName( this.PreviewDir );
			string _ImgDir = Path.GetFileName( this.ImgDir );

			return String.Format(
				//template
										_page_tpl,
				//name
										( _index != 0 ? _index.ToString() : "index" ),
				//gallery
										String.Concat(
											_pages.
												Select( Path.GetFileName ).
												Select( a =>
													String.Format(
														_entity_tpl,
														Path.Combine(
															_PreviewDir,
															PreviewPrefix + Path.GetFileNameWithoutExtension( a ) + '.' + this.Extension ),
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
	class CMPRR : IComparer<string> {
		public int Compare( string x, string y ) {
			return String.CompareOrdinal( x, y );
		}
	}
}
