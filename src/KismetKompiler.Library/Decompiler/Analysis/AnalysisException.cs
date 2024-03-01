﻿using System.Runtime.Serialization;

namespace KismetKompiler.Library.Decompiler.Analysis
{
    [Serializable]
    public class AnalysisException : Exception
    {
        public AnalysisException()
        {
        }

        public AnalysisException(string? message) : base(message)
        {
        }

        public AnalysisException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected AnalysisException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}