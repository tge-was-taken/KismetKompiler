﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KismetKompiler.Library.Parser
{
    public partial class KismetScriptParser
    {
        private static HashSet<string> _additionalKeywords = new()
        {
            "bool",
            "byte",
            "int",
            "float",
            "string",
            "void",
            "true",
            "false"
        };

        public static bool IsKeyword(string keyword)
        {
            return _additionalKeywords.Contains(keyword) ||
                _LiteralNames.Any(x => x?.Trim('\'') == keyword);
        }
    }
}
