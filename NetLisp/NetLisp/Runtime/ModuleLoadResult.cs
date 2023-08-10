using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public class ModuleLoadResult
    {
        public ModuleLoadStatus Status { get; set; }
        public string RelevantSourcePath { get; set; } // only set if Status is ModuleSourceNotFound or ModuleSourceError
        public LispToken LoadResult { get; set; } // only set if Status is Success

        public ModuleLoadResult(ModuleLoadStatus status, string relevantSourcePath = null, LispToken loadResult = null)
        {
            Status = status;
            RelevantSourcePath = relevantSourcePath;
            LoadResult = loadResult;
        }
    }
    public enum ModuleLoadStatus
    {
        Success,
        ModuleConfigNotFound,
        ModuleConfigInvalid,
        ModuleSourceNotFound,
        NativeSourceInvalid,
        ModuleSourceError // indicates a syntax or runtime error in a source in the module. appropriate events will be raised
    }
}
