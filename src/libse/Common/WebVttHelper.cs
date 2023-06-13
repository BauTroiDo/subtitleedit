﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Core.Common
{
    public static class WebVttHelper
    {
        private static readonly Regex NameRegex = new Regex("\\([\\.a-zA-Z\\d#_-]+\\)", RegexOptions.Compiled);
        private static readonly Regex PropertiesRegex = new Regex("{[ \\.a-zA-Z\\d:#\\s,_;:\\-\\(\\)]+}", RegexOptions.Compiled);

        public static List<WebVttStyle> GetStyles(Subtitle webVttSubtitle)
        {
            if (string.IsNullOrEmpty(webVttSubtitle.Header))
            {
                return new List<WebVttStyle>();
            }

            var cueOn = false;
            var styleOn = false;
            var result = new List<WebVttStyle>();
            var currentStyle = new StringBuilder();
            foreach (var line in webVttSubtitle.Header.SplitToLines())
            {
                var s = line.Trim();
                if (styleOn)
                {
                    if (s == string.Empty)
                    {
                        styleOn = false;
                        AddStyle(result, currentStyle);
                    }
                    else
                    {
                        if (cueOn && s.StartsWith("::cue(", StringComparison.Ordinal))
                        {
                            AddStyle(result, currentStyle);
                            currentStyle = new StringBuilder();
                        }

                        if (s.StartsWith("::cue(", StringComparison.Ordinal))
                        {
                            currentStyle.AppendLine(s);
                            cueOn = true;
                        }
                        else if (cueOn)
                        {
                            currentStyle.AppendLine(s);
                        }
                    }
                }
                else if (s.Equals("STYLE", StringComparison.OrdinalIgnoreCase))
                {
                    styleOn = true;
                }
            }

            AddStyle(result, currentStyle);

            return result;

            // https://www.w3.org/TR/webvtt1/
            // STYLE
            // ::cue {
            //   background-image: linear-gradient(to bottom, dimgray, lightgray);
            //   color: papayawhip;
            // }

            // STYLE
            // ::cue(b) {
            //   color: peachpuff;
            // }
        }

        private static void AddStyle(List<WebVttStyle> result, StringBuilder currentStyle)
        {
            var text = currentStyle
                .ToString()
                .Replace(Environment.NewLine, " ");
            var match = NameRegex.Match(text);
            if (!match.Success)
            {
                return;
            }

            var name = match.Value.Trim('(', ')', ' ');

            match = PropertiesRegex.Match(text);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Value))
            {
                return;
            }

            var properties = match.Value
                .Trim('{', '}', ' ')
                .RemoveChar('\r', '\n')
                .Split(';');

            var webVttStyle = new WebVttStyle { Name = name };
            foreach (var prop in properties)
            {
                SetProperty(webVttStyle, prop);
            }

            result.Add(webVttStyle);
        }

        private static void SetProperty(WebVttStyle webVttStyle, string prop)
        {
            var arr = prop.Split(':');
            if (arr.Length != 2)
            {
                return;
            }

            var name = arr[0].Trim();
            var value = arr[1].Trim();

            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (name == "color")
            {
                SetColor(webVttStyle, value);
            }
            else if (name == "background-color")
            {
                SetBackgroundColor(webVttStyle, value);
            }
            else if (name == "font-family")
            {
                webVttStyle.FontName = value;
            }
            else if (name == "font-style")
            {
                SetFontStyle(webVttStyle, value);
            }
            else if (name == "font-weight")
            {
                SetFontWeight(webVttStyle, value);
            }
            else if (name == "text-shadow")
            {
                SetTextShadow(webVttStyle, value);
            }
        }

        private static void SetColor(WebVttStyle webVttStyle, string value)
        {
            var color = GetColorFromString(value);
            if (!color.HasValue)
            {
                return;
            }

            webVttStyle.Color = color;
        }

        private static Color? GetColorFromString(string s)
        {
            try
            {
                if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
                {
                    var arr = s
                        .RemoveChar(' ')
                        .Remove(0, 4)
                        .TrimEnd(')')
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    return Color.FromArgb(int.Parse(arr[0]), int.Parse(arr[1]), int.Parse(arr[2]));
                }

                if (s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
                {
                    var arr = s
                        .RemoveChar(' ')
                        .Remove(0, 5)
                        .TrimEnd(')')
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    var alpha = byte.MaxValue;
                    if (arr.Length == 4 && float.TryParse(arr[3], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var f))
                    {
                        if (f >= 0 && f < 1)
                        {
                            alpha = (byte)(f * byte.MaxValue);
                        }
                    }

                    return Color.FromArgb(alpha, int.Parse(arr[0]), int.Parse(arr[1]), int.Parse(arr[2]));
                }

                return ColorTranslator.FromHtml(s);
            }
            catch
            {
                return null;
            }
        }

        private static void SetBackgroundColor(WebVttStyle webVttStyle, string value)
        {
            var color = GetColorFromString(value);
            if (!color.HasValue)
            {
                return;
            }

            webVttStyle.BackgroundColor = color;
        }

        private static void SetFontWeight(WebVttStyle webVttStyle, string value)
        {
            if (value == "bold" || value == "bolder")
            {
                webVttStyle.Bold = true;
            }
            else if (value == "normal")
            {
                webVttStyle.Bold = false;
            }
        }

        private static void SetFontStyle(WebVttStyle webVttStyle, string value)
        {
            if (value == "italic" || value == "oblique")
            {
                webVttStyle.Italic = true;
            }
            else if (value == "normal")
            {
                webVttStyle.Italic = false;
            }
        }

        private static void SetTextShadow(WebVttStyle webVttStyle, string value)
        {
            //  text-shadow: #101010 3px;

            var arr = value.Split();
            if (arr.Length != 2)
            {
                return;
            }

            var color = GetColorFromString(arr[0]);
            if (!color.HasValue)
            {
                return;
            }

            if (int.TryParse(arr[1].Replace("px", string.Empty), out var number))
            {
                webVttStyle.ShadowColor = color;
                webVttStyle.ShadowWidth = number;
            }
        }

        public static WebVttStyle GetStyleFromColor(Color color, Subtitle webVttSubtitle)
        {
            foreach (var style in GetStyles(webVttSubtitle))
            {
                if (style.Color.HasValue && style.Color.Value == color &&
                    style.BackgroundColor == null &&
                    style.Bold == null &&
                    style.Italic == null &&
                    style.FontName == null &&
                    style.FontSize == null &&
                    style.Underline == null)
                {
                    return style;
                }
            }

            return null;
        }

        public static WebVttStyle AddStyleFromColor(Color color)
        {
            return new WebVttStyle
            {
                Name = Utilities.ColorToHexWithTransparency(color).TrimStart('#'),
                Color = color,
            };
        }

        public static string AddStyleToHeader(string header, WebVttStyle style)
        {
            var rawStyle = "::cue(." + style.Name + ") { " + GetCssProperties(style) + " }";

            if (string.IsNullOrEmpty(header))
            {
                return "STYLE" + Environment.NewLine + rawStyle;
            }

            if (header.Contains("::cue(." + style.Name + ")"))
            {
                return header;
            }

            var sb = new StringBuilder();
            var styleFound = false;
            foreach (var line in header.SplitToLines())
            {
                sb.AppendLine(line);
                if (line.Trim() == "STYLE" && !styleFound)
                {
                    sb.AppendLine(rawStyle);
                    styleFound = true;
                }
            }

            if (!styleFound)
            {
                sb.AppendLine();
                sb.AppendLine("STYLE");
                sb.AppendLine(rawStyle);
            }

            return sb.ToString();
        }

        private static string GetCssProperties(WebVttStyle style)
        {
            var sb = new StringBuilder();

            if (style.Color != null)
            {
                sb.Append($"color:rgba({style.Color.Value.R},{style.Color.Value.G},{style.Color.Value.B},{(style.Color.Value.A / 255.0).ToString(CultureInfo.InvariantCulture)}); ");
            }

            if (style.BackgroundColor != null)
            {
                sb.Append($"background-color:rgba({style.BackgroundColor.Value.R},{style.BackgroundColor.Value.G},{style.BackgroundColor.Value.B},{(style.BackgroundColor.Value.A / 255.0).ToString(CultureInfo.InvariantCulture)}); ");
            }

            if (style.Italic != null && style.Italic.Value == true)
            {
                sb.Append("font-style:italic; ");
            }

            if (style.Bold != null && style.Bold.Value == true)
            {
                sb.Append("font-weight:bold; ");
            }

            return sb.ToString().TrimEnd(' ', ';');
        }

        public static string RemoveColorTag(string input, Color color, List<WebVttStyle> webVttStyles)
        {
            if (webVttStyles == null)
            {
                return input;
            }

            var style = webVttStyles.FirstOrDefault(p => p.Color == color &&
                                                         p.Italic == null &&
                                                         p.Bold == null);
            if (style == null)
            {
                return input;
            }

            return AddStyleToText(input, style);
        }

        public static string AddStyleToText(string input, WebVttStyle style)
        {
            var text = input;
            var idx = text.IndexOf("<c." + style.Name + ">", StringComparison.Ordinal);
            if (idx >= 0)
            {
                text = text.Replace("<c." + style.Name + ">", string.Empty);
                idx = text.IndexOf("</c>", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    text = text.Remove(idx, 4);
                }
            }
            else
            {
                text = text.Replace("." + style.Name, string.Empty);
            }

            return text;
        }
    }
}
