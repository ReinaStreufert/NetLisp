using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public interface INativeSource
    {
        public LispToken OnSourceLoad(RuntimeContext runtimeContext);

        public static bool TryLoadFromFile(string filePath, string innerType, out INativeSource loadedSource)
        {
            Assembly sourceAssembly;
            Type nativeSourceType;
            try
            {
                sourceAssembly = Assembly.LoadFrom(filePath);
                nativeSourceType = sourceAssembly.GetType(innerType);
            } catch
            {
                loadedSource = null;
                return false;
            }
            if (nativeSourceType == null || !nativeSourceType.IsAssignableTo(typeof(INativeSource)))
            {
                loadedSource = null;
                return false;
            }
            object? inst = Activator.CreateInstance(nativeSourceType);
            if (inst == null)
            {
                loadedSource = null;
                return false;
            }
            loadedSource = (INativeSource)inst;
            return true;
        }
    }
}
