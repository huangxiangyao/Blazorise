﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ColorCode;

namespace Blazorise.Docs.Compiler
{
    public class ExamplesMarkup
    {
        public bool Execute()
        {
            var paths = new Paths();
            var newFiles = new StringBuilder();
            var success = true;
            var noOfFilesUpdated = 0;
            var noOfFilesCreated = 0;

            try
            {
                var formatter = new HtmlClassFormatter();
                var lastCheckedTime = new DateTime();
                if ( File.Exists( paths.NewFilesToBuildPath() ) )
                {
                    var lastNewFilesToBuild = new FileInfo( paths.NewFilesToBuildPath() );
                    lastCheckedTime = lastNewFilesToBuild.LastWriteTime;
                }

                var dirPath = paths.DirPath();
                var directoryInfo = new DirectoryInfo( dirPath );

                var razorFiles = directoryInfo.GetFiles( "*.razor", SearchOption.AllDirectories );
                var snippetFiles = directoryInfo.GetFiles( "*.snippet", SearchOption.AllDirectories );
                var csharpFiles = directoryInfo.GetFiles( "*.csharp", SearchOption.AllDirectories );

                foreach ( var entry in razorFiles.Concat( snippetFiles ).Concat( csharpFiles ) )
                {
                    if ( entry.Name.EndsWith( "Code.razor" ) )
                    {
                        continue;
                    }

                    if ( !entry.Name.Contains( Paths.ExampleDiscriminator ) )
                        continue;

                    var markupPath = entry.FullName
                        .Replace( "Examples", "Code" )
                        .Replace( ".razor", "Code.html" )
                        .Replace( ".snippet", "Code.html" )
                        .Replace( ".csharp", "Code.html" );

                    if ( entry.LastWriteTime < lastCheckedTime && File.Exists( markupPath ) )
                    {
                        continue;
                    }

                    var markupDir = Path.GetDirectoryName( markupPath );
                    if ( !Directory.Exists( markupDir ) )
                    {
                        Directory.CreateDirectory( markupDir );
                    }

                    var cb = new CodeBuilder();
                    var currentCode = string.Empty;
                    var isCSharp = entry.FullName.EndsWith( ".csharp" );
                    var src = StripComponentSource( entry.FullName );

                    if ( isCSharp )
                    {
                        cb.AddLine( "<div class=\"blazorise-codeblock\">" );

                        cb.AddLine(
                            formatter.GetHtmlString( src, Languages.CSharp )
                                .Replace( "@", "<span class=\"atSign\">&#64;</span>" )
                                .ToLfLineEndings() );

                        cb.AddLine( "</div>" );
                    }
                    else
                    {
                        var blocks = src.Split( "@code" );

                        var blocks0 = Regex.Replace( blocks[0], @"</?DocsFrame>", string.Empty )
                            .Replace( "@", "PlaceholdeR" )
                            .Trim();

                        // Note: the @ creates problems and thus we replace it with an unlikely placeholder and in the markup replace back.
                        var html = formatter.GetHtmlString( blocks0, Languages.Html ).Replace( "PlaceholdeR", "@" );
                        html = AttributePostprocessing( html ).Replace( "@", "<span class=\"atSign\">&#64;</span>" );

                        if ( File.Exists( markupPath ) )
                        {
                            currentCode = File.ReadAllText( markupPath );
                        }

                        cb.AddLine( "<div class=\"blazorise-codeblock\">" );
                        cb.AddLine( html.ToLfLineEndings() );

                        if ( blocks.Length == 2 )
                        {
                            cb.AddLine(
                                formatter.GetHtmlString( "@code" + blocks[1], Languages.CSharp )
                                    .Replace( "@", "<span class=\"atSign\">&#64;</span>" )
                                    .ToLfLineEndings() );
                        }

                        cb.AddLine( "</div>" );
                    }

                    if ( currentCode != cb.ToString() )
                    {
                        File.WriteAllText( markupPath, cb.ToString() );
                        if ( currentCode == string.Empty )
                        {
                            newFiles.AppendLine( markupPath );
                            noOfFilesCreated++;
                        }
                        else
                        {
                            noOfFilesUpdated++;
                        }
                    }
                }

                File.WriteAllText( paths.NewFilesToBuildPath(), newFiles.ToString() );
            }
            catch ( Exception e )
            {
                Console.WriteLine( $"Error generating examples markup : {e.Message}" );
                success = false;
            }

            Console.WriteLine( $"Docs.Compiler updated {noOfFilesUpdated} generated files" );
            Console.WriteLine( $"Docs.Compiler generated {noOfFilesCreated} new files" );
            return success;
        }

        private static string StripComponentSource( string path )
        {
            var source = File.ReadAllText( path, Encoding.UTF8 );
            source = Regex.Replace( source, "@(namespace|layout|page) .+?\n", string.Empty );
            return source.Trim();
        }

        public static string AttributePostprocessing( string html )
        {
            return Regex.Replace(
                html,
                @"<span class=""htmlAttributeValue"">&quot;(?'value'.*?)&quot;</span>",
                new MatchEvaluator( m =>
                     {
                         var value = m.Groups["value"].Value;
                         return
                             $@"<span class=""quot"">&quot;</span>{AttributeValuePostprocessing( value )}<span class=""quot"">&quot;</span>";
                     } ) );
        }

        private static string AttributeValuePostprocessing( string value )
        {
            if ( string.IsNullOrWhiteSpace( value ) )
                return value;
            if ( value == "true" || value == "false" )
                return $"<span class=\"keyword\">{value}</span>";
            if ( Regex.IsMatch( value, "^[A-Z][A-Za-z0-9]+[.][A-Za-z][A-Za-z0-9]+$" ) )
            {
                var tokens = value.Split( '.' );
                return $"<span class=\"enum\">{tokens[0]}</span><span class=\"enumValue\">.{tokens[1]}</span>";
            }

            if ( Regex.IsMatch( value, "^@[A-Za-z0-9]+$" ) )
            {
                return $"<span class=\"sharpVariable\">{value}</span>";
            }

            return $"<span class=\"htmlAttributeValue\">{value}</span>";
        }
    }
}
