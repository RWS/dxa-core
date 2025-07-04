﻿using System;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Compile
{
    /// <summary>
    /// Compile Exception
    /// </summary>
    public class CompileException : Exception
    {
        public CompileException(string msg) : base(msg)
        {
        }

        public CompileException(string msg, Exception innerException) : base(msg, innerException)
        {
        }
    }
}
