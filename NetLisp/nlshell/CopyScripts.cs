using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlshell
{
    // ignore this trash bullshit that is only relevent to the directory structure on my personal
    // laptop

    static class CopyScripts
    {
        const string baseModulePath = @"C:\Users\Reina\Documents\GitHub\NetLisp\NetLisp\nlispcr\bin\Debug\net7.0\modules\";
        const string baseProductionPath = @"C:\Users\Reina\Documents\GitHub\NetLisp\NetLisp\NetLisp Standard Library\";

        public static void Copy(CopyFilter filter = CopyFilter.All, params string[] moduleNames)
        {
            List<string> destPaths = new List<string>();
            if (moduleNames.Length > 0)
            {
                foreach (string moduleName in moduleNames)
                {
                    destPaths.AddRange(getModuleDestPaths(baseModulePath + moduleName, filter));
                }
            } else
            {
                foreach (string modulePath in Directory.GetDirectories(baseModulePath))
                {
                    destPaths.AddRange(getModuleDestPaths(modulePath, filter));
                }
            }
            HashSet<string> copiedDestPaths = new HashSet<string>();
            foreach ((string sourcePath, string destPath) in findSources(baseProductionPath, destPaths))
            {
                File.Copy(sourcePath, destPath, true);
                copiedDestPaths.Add(destPath);
            }
            foreach (string destPath in destPaths)
            {
                if (!copiedDestPaths.Contains(destPath))
                {
                    Console.WriteLine("Could not find a source path for dest path " + destPath);
                }
            }
        }
        private static IEnumerable<(string sourcePath, string destPath)> findSources(string baseDir, List<string> destPaths)
        {
            foreach (string subFile in Directory.GetFiles(baseDir))
            {
                string? matchingDestPath = destPaths.Where((string destPath) => Path.GetFileName(subFile) == Path.GetFileName(destPath)).FirstOrDefault((string)null);
                if (matchingDestPath != null)
                {
                    yield return (subFile, matchingDestPath);
                }
            }
            foreach (string subDir in Directory.GetDirectories(baseDir))
            {
                const string dllSubPath = @"\bin\Debug\net7.0\";
                foreach (string destPath in destPaths)
                {
                    string potentialSourcePath = subDir + dllSubPath + Path.GetFileName(destPath);
                    if (File.Exists(potentialSourcePath))
                    {
                        yield return (potentialSourcePath, destPath);
                    }
                }
            }
        }
        private static IEnumerable<string> getModuleDestPaths(string modulePath, CopyFilter filter)
        {
            foreach (string filePath in Directory.GetFiles(modulePath))
            {
                if (filter == CopyFilter.All)
                {
                    yield return filePath;
                } else if (filter == CopyFilter.DllsOnly)
                {
                    if (Path.GetExtension(filePath) == ".dll")
                    {
                        yield return filePath;
                    }
                } else if (filter == CopyFilter.ScriptsOnly)
                {
                    if (Path.GetExtension(filePath) == ".nlisp")
                    {
                        yield return filePath;
                    }
                }
            }
        }
    }
    enum CopyFilter
    {
        All,
        ScriptsOnly,
        DllsOnly
    }
}
