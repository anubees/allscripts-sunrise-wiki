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
    /// TablesForMarkdown Plugin
    /// </summary>
    /// <remarks>
    /// The mapping between the Emoticons(images) and the Notations is stored in Plugins/EmoticonsPlugin/EmoticonsMappings.xml
    /// Note: The image path can be changed in the above XML
    ///       However, if you want to change the notation in the XML, the regex for the notation also needs to be updated in the method - AfterParse below.
    /// </remarks>
	public class TablesForMarkdown : TextPlugin
	{
        string noWikiEscapeStart = "{{{";
        string noWikiEscapeEnd = "}}}";
        Dictionary<string, string> HTMLAttributes = new Dictionary<string, string>();

		public override string Id
		{
            get { return "TablesForMarkdown"; }
		}

		public override string Name
		{
            get { return "TablesForMarkdown"; }
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

        public TablesForMarkdown()
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
            html = TranslateNotation(html);

            return html;
		}

        /// <summary>
        /// Translate Correct Notation src="@Url.Content("~/Assets/Images/AllscriptsSparkWikiLogo.png")"
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private string TranslateNotation(string html)
        {

            // all of the syntax of flowing formatting markup across "soft" paragraph breaks gets a lot easier if we just merge soft breaks together.
			// This is what _mergeLines does, giving us an array of "lines" which can be processed for line based formatting such as ==, etc.
			List<int> originalLineNumbers;
            List<string> lines = _breakMarkupIntoLines(html, out originalLineNumbers);
			StringBuilder htmlMarkup = new StringBuilder();

			int iBullet = 0;    // bullet indentation level
			int iNumber = 0;    // ordered list indentation level
			bool InTable = false;  // are in a table definition
			bool InEscape = false; // we are in an escaped section
			int idParagraph = 1;  // id for paragraphs
			bool inRoadkillEscape = false;

			// process each line of the markup, since bullets, numbers and tables depend on start of line
			foreach (string l in lines)
			{
				// make a copy as we will modify line into HTML as we go
				string line = l.Trim('\r');
				string lineTrimmed = line.TrimStart(' ');

                if (!(lineTrimmed.StartsWith("|") || lineTrimmed.StartsWith("<p>|")) && !InTable)
                {
                    htmlMarkup.Append(lineTrimmed);
                    continue;
                }

				// if we aren't in an escaped section
				if (!InEscape && !inRoadkillEscape)
				{
					// if we were in a table definition and this isn't another row 
					if ((InTable) && (string.IsNullOrEmpty(lineTrimmed) || (lineTrimmed.Length > 0 && lineTrimmed[0] != '|')))
					{
						// then we close the table out
						htmlMarkup.Append("</table></div>");
						InTable = false;
					}                        					
					// --- start of table with header row
                    else if (!InTable && (lineTrimmed.StartsWith("|=") || lineTrimmed.StartsWith("<p>|=")))
					{
						// close any pending lists
						_closeLists(ref htmlMarkup, ref iBullet, ref iNumber, lineTrimmed);

						// start a new table
						htmlMarkup.Append(_getStartTag("<div><table class=\"wikitable\">"));
						InTable = true;
						htmlMarkup.Append(_processTableHeaderRow(lineTrimmed));
					}
					// --- start of table - standard row
					else if (!InTable && lineTrimmed[0] == '|')
					{
						// close any pending lists
						_closeLists(ref htmlMarkup, ref iBullet, ref iNumber, lineTrimmed);

						// start a new table
						htmlMarkup.Append(_getStartTag("<table class=\"wikitable\">"));
						InTable = true;
						htmlMarkup.Append(_processTableRow(lineTrimmed));
					}
					// --- new header row in table
					else if (InTable && lineTrimmed.StartsWith("|="))
					{
						// we are already processing table so this must be a new header row
						htmlMarkup.Append(_processTableHeaderRow(lineTrimmed));
					}
					// --- new standard row in table
					else if (InTable && lineTrimmed[0] == '|')
					{
						// we are already processing table so this must be a new row
						htmlMarkup.Append(_processTableRow(lineTrimmed));
					}
					else if (lineTrimmed.Contains(TextPlugin.PARSER_IGNORE_STARTTOKEN))
					{
						inRoadkillEscape = true;
					}					
				}
				else
				{
					if (lineTrimmed.Contains(TextPlugin.PARSER_IGNORE_ENDTOKEN))
					{
						inRoadkillEscape = false;
					}					
				}
				idParagraph++;
			}
			// close out paragraph
			//htmlMarkup.Append("</div>");

			// return the HTML we have generated 
			return htmlMarkup.ToString();
        }

        private List<string> _breakMarkupIntoLines(string markup, out List<int> originalLineNumbers)
        {
            originalLineNumbers = new List<int>();
            List<string> lines = new List<string>();
            char[] chars = { '\n' };
            // break the creole into lines so we can process each line  
            string[] tempLines = markup.Split(chars);
            bool InEscape = false; // we are in a preformated escape
            // all markup works on a per line basis EXCEPT for the contiuation of lines with simple CR, so we simply merge those in, which makes a 
            // much easier processing story later on
            for (int iLine = 0; iLine < tempLines.Length; iLine++)
            {
                string line = tempLines[iLine];
                int i = iLine + 1;

                if ((line.Length > 0) && (line != "\r") && line[0] != '=')
                {
                    if (!InEscape)
                    {
                        if (line.StartsWith(noWikiEscapeStart))
                        {
                            InEscape = true;
                        }
                        else
                        {
                            // merge all lines which don't start with a command line together
                            while (true)
                            {
                                if (i == tempLines.Length)
                                {
                                    iLine = i - 1;
                                    break;
                                }

                                string trimmedLine = tempLines[i].Trim();
                                if ((trimmedLine.Length == 0) ||
                                    trimmedLine[0] == '\r' ||
                                    trimmedLine[0] == '#' ||
                                    trimmedLine[0] == '*' ||
                                    trimmedLine.StartsWith(TextPlugin.PARSER_IGNORE_STARTTOKEN) ||
                                    trimmedLine.StartsWith(noWikiEscapeStart) ||
                                    trimmedLine[0] == '=' ||
                                    trimmedLine.StartsWith("----") ||
                                    trimmedLine[0] == '|')
                                {
                                    iLine = i - 1;
                                    break;
                                }
                                line += " " + trimmedLine; // erg, does CR == whitespace?
                                i++;
                            }
                        }
                    }
                    else
                    {
                        if (line.StartsWith(noWikiEscapeEnd))
                            InEscape = false;
                    }
                }
                // add the merged line to our list
                originalLineNumbers.Add(iLine);
                lines.Add(line);
            }
            originalLineNumbers.Add(lines.Count - 1);
            return lines;
        }
        protected string _getStartTag(string tag)
        {
            if (HTMLAttributes.ContainsKey(tag))
                return HTMLAttributes[tag];
            return tag;
        }

        private void _closeLists(ref StringBuilder htmlMarkup, ref int iBullet, ref int iNumber, string lineTrimmed)
        {
            if (lineTrimmed.Length > 0)
            {
                // if we were doing an ordered list and this isn't one, we need to close the previous list down
                if ((iNumber > 0) && lineTrimmed[0] != '#')
                    htmlMarkup.Append(_closeList(ref iNumber, "</ol>"));

                // if we were doing an bullet list and this isn't one, we need to close the previous list down
                if ((iBullet > 0) && lineTrimmed[0] != '*')
                    htmlMarkup.Append(_closeList(ref iBullet, "</ul>"));
            }
            else
            {
                // if we were doing an ordered list and this isn't one, we need to close the previous list down
                if (iNumber > 0)
                    htmlMarkup.Append(_closeList(ref iNumber, "</ol>"));

                // if we were doing an bullet list and this isn't one, we need to close the previous list down
                if (iBullet > 0)
                    htmlMarkup.Append(_closeList(ref iBullet, "</ul>"));
            }
        }

        private string _processTableRow(string line)
        {
            string markup = _getStartTag("<tr>");
            int iPos = _indexOfWithSkip(line, "|", 0);
            while (iPos >= 0)
            {
                iPos += 1;
                int iEnd = _indexOfWithSkip(line, "|", iPos);
                if (iEnd >= iPos)
                {
                    string cell = _processCreoleFragment(line.Substring(iPos, iEnd - iPos)).Trim();
                    if (cell.Length == 0)
                        cell = "&nbsp;"; // table won't render if there isn't at least something...
                    markup += String.Format("{0}{1}</td>", _getStartTag("<td align='center'>"), cell);
                    iPos = iEnd;
                }
                else
                    break;
            }
            markup += "</tr>";
            return markup;
        }

        private int _indexOfWithSkip(string markup, string match, int iPos)
        {
            bool fSkipLink = (match != "[[") && (match != "]]");
            bool fSkipEscape = (match != noWikiEscapeStart) && (match != noWikiEscapeEnd);
            bool fSkipImage = (match != "{{") && (match != "}}");

            int tokenLength = match.Length;
            if (tokenLength < 3)
                tokenLength = noWikiEscapeStart.Length; // so we can match on {{{
            for (int i = 0; i <= markup.Length - match.Length; i++)
            {
                if ((markup.Length - i) < tokenLength)
                    tokenLength = markup.Length - i;
                string token = markup.Substring(i, tokenLength);
                if (fSkipEscape && token.StartsWith(noWikiEscapeStart))
                {
                    // skip escape
                    int iEnd = markup.IndexOf(noWikiEscapeEnd, i);
                    if (iEnd > 0)
                    {
                        i = iEnd + 2; // plus for loop ++
                        continue;
                    }
                }
                if (fSkipLink && token.StartsWith("[["))
                {
                    // skip link
                    int iEnd = markup.IndexOf("]]", i);
                    if (iEnd > 0)
                    {
                        i = iEnd + 1; // plus for loop ++
                        continue;
                    }
                }
                if (fSkipImage && token.StartsWith("{{"))
                {
                    // skip image
                    int iEnd = markup.IndexOf("}}", i);
                    if (iEnd > 0)
                    {
                        i = iEnd + 1; // plus for loop ++
                        continue;
                    }
                }
                if (token.StartsWith(match))
                {
                    // make sure previous char is not a ~, for this we have to go back 2 chars as double ~ is an escaped escape char
                    if (i > 2)
                    {
                        string tildeCheck = markup.Substring(i - 2, 2);
                        if ((tildeCheck != "~~") && (tildeCheck[1] == '~'))
                            continue; // then we don't want to match this...it's been escaped
                    }

                    // only if it starts past our starting point
                    if (i >= iPos)
                        return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// passed a table definition line which starts with "|=" it outputs the start of a table definition
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private string _processTableHeaderRow(string line)
        {
            string markup = "";
            // add header
            markup += String.Format("{0}\n{1}\n", _getStartTag("<thead>"), _getStartTag("<tr>"));

            // process each |= cell section
            int iPos = _indexOfWithSkip(line, "|=", 0);
            while (iPos >= 0)
            {
                int iEnd = _indexOfWithSkip(line, "|=", iPos + 1);
                string cell = "";
                if (iEnd > iPos)
                {
                    iPos += 2;
                    cell = line.Substring(iPos, iEnd - iPos);
                    iPos = iEnd;
                }
                else
                {
                    iPos += 2;
                    if (line.Length - iPos > 0)
                        cell = line.Substring(iPos).TrimEnd(new char[] { '|' });
                    else
                        cell = "";
                    iPos = -1;
                }
                if (cell.Length == 0)
                    // forces the table cell to always be rendered
                    cell = "&nbsp;";

                // create cell entry
                markup += String.Format("{0}{1}</th>", _getStartTag("<th>"), _processCreoleFragment(cell));
            }
            // close up row and header
            markup += "</tr>\n</thead>\n";
            return markup;
        }

        private string _processListIndentations(string line, char indentMarker, ref int iCurrentIndent, string indentTag, string outdentTag)
        {
            string markup = "";
            int iNewIndent = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == indentMarker)
                    iNewIndent++;
                else
                    break;
            }
            // strip off counters
            line = line.Substring(iNewIndent);

            // close down bullets if we have fewer *s
            while (iNewIndent < iCurrentIndent)
            {
                markup += String.Format("{0}\n", outdentTag);
                iCurrentIndent--;
            }

            // add bullets if we have more *s
            while (iNewIndent > iCurrentIndent)
            {
                markup += String.Format("{0}\n", indentTag);
                iCurrentIndent++;
            }
            // mark the line in the list, processing the inner fragment for any additional markup
            markup += String.Format("{0}{1}</li>\n", _getStartTag("<li>"), _processCreoleFragment(line));
            return markup;
        }

        /// <summary>
        /// Given the current indentation level, close out the list
        /// </summary>
        /// <param name="iNumber"></param>
        private string _closeList(ref int iIndent, string closeHTML)
        {
            string html = "";
            while (iIndent > 0)
            {
                html += string.Format("{0}\n", closeHTML);
                iIndent--;
            }
            return html;
        }

        private string _processFreeLink(string schema, string markup)
        {
            int iPos = _indexOfWithSkip(markup, schema, 0);
            while (iPos >= 0)
            {
                string href = "";
                int iEnd = _indexOfWithSkip(markup, " ", iPos);
                if (iEnd > iPos)
                {
                    href = markup.Substring(iPos, iEnd - iPos);
                    string anchor = String.Format("<a target=\"_blank\" href=\"{0}\">{0}</a>", href);
                    markup = markup.Substring(0, iPos) + anchor + markup.Substring(iEnd);
                    iPos = iPos + anchor.Length;
                }
                else
                {
                    href = markup.Substring(iPos);
                    markup = markup.Substring(0, iPos) + String.Format("<a target=\"_blank\" href=\"{0}\">{0}</a>", href);
                    break;
                }
                iPos = _indexOfWithSkip(markup, schema, iPos);
            }
            return markup;
        }

        /// <summary>
        /// Process http:, https: ftp: links automatically into a hrefs
        /// </summary>
        /// <param name="markup"></param>
        /// <returns></returns>
        private string _processFreeLinks(string markup)
        {
            markup = _processFreeLink("ftp:", markup);
            markup = _processFreeLink("http:", markup);
            return _processFreeLink("https:", markup);
        }

        protected string _processCreoleFragment(string fragment)
        {          
            return fragment;
        }
	}
}
