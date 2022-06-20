using System;
using System.Collections.Generic;
using PyStubblerAnnotations;

namespace PyStubblerLib
{

    public class BuildConfig
    {

        public string Prefix { get; set; } = string.Empty;
        public string Postfix { get; set; } = string.Empty;
        public bool DestPathIsRoot { get; set; } = false;
    }
}
