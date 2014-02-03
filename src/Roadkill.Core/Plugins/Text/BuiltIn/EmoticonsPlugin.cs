using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roadkill.Core.Configuration;
using Roadkill.Core.Database;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.IO;

namespace Roadkill.Core.Plugins.Text.BuiltIn
{
    /// <summary>
    /// Emoticons Plugin
    /// </summary>
    /// <remarks>
    /// The mapping between the Emoticons(images) and the Notations is stored in Plugins/EmoticonsPlugin/EmoticonsMappings.xml
    /// Note: The image path can be changed in the above XML
    ///       However, if you want to change the notation in the XML, the regex for the notation also needs to be updated in the method - AfterParse below.
    /// </remarks>
	public class EmoticonsPlugin : TextPlugin
	{
		public override string Id
		{
            get { return "EmoticonsPlugin"; }
		}

		public override string Name
		{
			get { return "Emoticons"; }
		}

		public override string Description
		{
			get { return "Add Emoticons to your pages using notations such as {:)}, {;)}, etc"; }
		}

		public override string Version
		{

			get
			{
				return "1.0";
			}
		}

        public EmoticonsPlugin()
		{
			
		}

        /// <summary>
        /// After Parse
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        /// <remarks></remarks>
		public override string AfterParse(string html)
		{
            //Note: A notation enclosed in '[' ']' is escaped.

            //1. Translate {:)} to <img src='{0}'/>, Ignore notations starting with
            //   '[' and ending with ']' i.e. [{:)}]
            html = TranslateCorrectNotation(html);
            //2. Translate [{:)}] to {:)} - This can be used for creating a Help page
            //   where the notations need to be escaped and not translated into the corresponding smiley images
            html = TranslateEscapedNotations(html);

            return html;
		}

        /// <summary>
        /// Translate Correct Notation src="@Url.Content("~/Assets/Images/AllscriptsSparkWikiLogo.png")"
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private string TranslateCorrectNotation(string html)
        {
            const RegexOptions myRegexOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            //Regex for a correct Notation - This notation will be replaced by an image
            //E.g: {:)}
            //(?<!\[) skips Notations that start with '[' 
            //(?!\]) skips Notations that end with ']'
            //E.g: [{:)}]
            const string regexCorrect = @"(?<!\[)\{(\:[()DP]|\;\)|(\(([ynix!+\-?*\/]|on|off|\*[rgby])\)))\}(?!\])";
            Regex myRegexCorrect = new Regex(regexCorrect, myRegexOptions);
            const string imageLocation = @"<img src='{0}' />";
            int advanceIndex = 0;
            foreach (Match myMatch in myRegexCorrect.Matches(html))
            {
                if (myMatch.Success)
                {
                    Group group = myMatch.Groups[0];
                    string mappedPath = System.Web.VirtualPathUtility.ToAbsolute(GetImageForNotation(group.Value));
                    string imageReplacement = string.Format(imageLocation, mappedPath);
                    html = html.Substring(0, group.Index + advanceIndex) + imageReplacement + html.Substring(group.Index + advanceIndex + group.Length);
                    advanceIndex += imageReplacement.Length - group.Length;
                }
            }
            return html;
        }

        /// <summary>
        /// Translate Escaped Notations
        /// </summary>
        /// <param name="html"></param>        
        /// <returns></returns>
        private static string TranslateEscapedNotations(string html)
        {
            const RegexOptions myRegexOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            //Regex for ignored/skipped Notation - This notation will be replaced by a notation without the '[' and ']'
            //E.g: [{:)}]
            const string regexEscape = @"\[\{(\:[()DP]|\;\)|(\(([ynix!+\-?*\/]|on|off|\*[rgby])\)))\}\]";
            Regex myRegexEscape = new Regex(regexEscape, myRegexOptions);

            int backTrackIndex = 0;
            foreach (Match myMatch in myRegexEscape.Matches(html))
            {
                if (myMatch.Success)
                {
                    char[] charsToTrim = { '[', ']' };
                    Group group = myMatch.Groups[0];
                    html = html.Substring(0, group.Index - backTrackIndex) + group.Value.Trim(charsToTrim) + html.Substring(group.Index - backTrackIndex + group.Length);
                    backTrackIndex += group.Length - group.Value.Trim(charsToTrim).Length;
                }
            }
            return html;
        }


        /// <summary>
        /// Get Image For Notation
        /// </summary>
        /// <param name="notation"></param>
        /// <returns></returns>
        private string GetImageForNotation(string notation)
        {
            XElement xelement = XElement.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Plugins/EmoticonsPlugin/EmoticonsMappings.xml"));
            IEnumerable<XElement> mappings = xelement.Elements();
            var imagePath = from mapping in mappings where (string)mapping.Element("Notation") == notation
                            select mapping.Element("Image");

            return imagePath.FirstOrDefault().Value;
        }
	}
}
