using NetLisp.Data;
using NetLisp.Runtime;
using NetLisp.Text.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlshell
{
    public static class AutocompleteSource
    {
        public abstract class AutocompleteCache
        {
            public static AutocompleteCache CreatePathCache()
            {
                return new PathAutocompleteCache();
            }
        }
        private class PathAutocompleteCache : AutocompleteCache
        {
            public string? lastDir = null;
            public string[]? lastSubDirs = null;
            public string[]? lastSubFiles = null;
        }
        public static IEnumerable<AutocompleteOption> FromFilesystemPath(AutocompleteCache cache, string startText)
        {
            PathAutocompleteCache pathCache = (PathAutocompleteCache)cache;
            string[] startTextSegments = startText.Split('/', '\\');
            string startTextDir;
            if (startTextSegments.Length < 2)
            {
                startTextDir = Environment.CurrentDirectory;
            } else
            {
                StringBuilder startTextDirBuilder = new StringBuilder();
                for (int i = 0; i < startTextSegments.Length - 1; i++)
                {
                    startTextDirBuilder.Append(startTextSegments[i]);
                    startTextDirBuilder.Append(Path.DirectorySeparatorChar);
                }
                startTextDir = startTextDirBuilder.ToString();
            }
            if (pathCache.lastDir != startTextDir)
            {
                pathCache.lastDir = startTextDir;
                if (Directory.Exists(startTextDir))
                {
                    try
                    {
                        pathCache.lastSubFiles = Directory.GetFiles(startTextDir).Select((string fullPath) => Path.GetFileName(fullPath)).ToArray();
                        pathCache.lastSubDirs = Directory.GetDirectories(startTextDir).Select((string fullPath) => Path.GetFileName(fullPath)).ToArray();
                    } catch (UnauthorizedAccessException)
                    {
                        pathCache.lastSubFiles = null;
                        pathCache.lastSubDirs = null;
                    }
                } else
                {
                    pathCache.lastSubDirs = null;
                    pathCache.lastSubFiles = null;
                }
            }
            if (pathCache.lastSubDirs == null)
            {
                yield break;
            }
            string partialSegment = startTextSegments[startTextSegments.Length - 1].ToLower();
            string preferredDelim = "\\\\";
            if (startText.Contains('/'))
            {
                preferredDelim = "/";
            }
            if ("..".StartsWith(partialSegment))
            {
                yield return new AutocompleteOption("..", "..".Substring(partialSegment.Length) + preferredDelim);
            }
            foreach (string subName in pathCache.lastSubFiles)
            {
                if (subName.ToLower().StartsWith(partialSegment))
                {
                    yield return new AutocompleteOption(subName, subName.Substring(partialSegment.Length));
                }
            }
            foreach (string subName in pathCache.lastSubDirs)
            {
                if (subName.ToLower().StartsWith(partialSegment))
                {
                    yield return new AutocompleteOption(subName, subName.Substring(partialSegment.Length) + preferredDelim);
                }
            }
        }
        public static IEnumerable<AutocompleteOption> FromScopeAnalysis(ListScopeAnalysis listScope, string startText, MetadataTypeRestrictions typeRestrictions, ScopeAnalysisAutocompleteType searchType)
        {
            foreach (AutocompleteOption option in fromScope(listScope.InnerGlobalScope, startText, typeRestrictions, searchType))
            {
                yield return option;
            }
            foreach (AutocompleteOption option in fromScope(listScope.InnerBuiltScope, startText, typeRestrictions, searchType))
            {
                yield return option;
            }
        }
        private static IEnumerable<AutocompleteOption> fromScope(Scope scope, string startText, MetadataTypeRestrictions typeRestrictions, ScopeAnalysisAutocompleteType searchType)
        {
            if (typeRestrictions.IsNoType)
            {
                yield break;
            }
            foreach (KeyValuePair<string, LispToken?> pair in scope.AllDefinedNames())
            {
                if (pair.Key.StartsWith(startText))
                {
                    if (pair.Value == null || (typeRestrictions.IsAnyType && searchType == ScopeAnalysisAutocompleteType.All))
                    {
                        yield return new AutocompleteOption(pair.Key, pair.Key.Substring(startText.Length));
                        continue;
                    }
                    LispToken nameValue = pair.Value;
                    if (searchType == ScopeAnalysisAutocompleteType.MatchingValues)
                    {
                        LispTypeInfo nameValueType = nameValue.GetTypeInfo();
                        if (nameValueType == AnalysisToken.AnalysisTokenExtendedTypeInfo)
                        {
                            if (typeRestrictions.CanBePassed(((AnalysisToken)nameValue).TokenTypeRestriction))
                            {
                                yield return new AutocompleteOption(pair.Key, pair.Key.Substring(startText.Length));
                            }
                        } else
                        {
                            if (typeRestrictions.CanBePassed(nameValueType))
                            {
                                yield return new AutocompleteOption(pair.Key, pair.Key.Substring(startText.Length));
                            }
                        }
                    } else if (searchType == ScopeAnalysisAutocompleteType.MatchingRoutines)
                    {
                        if (nameValue.TypeCanBeExecuted)
                        {
                            ExecutableLispToken executableValue = (ExecutableLispToken)nameValue;
                            MetadataTypeRestrictions returnTypeRestriction = executableValue.Metadata.ReturnParam.TypeRestrictions;
                            if (typeRestrictions.CanBePassed(returnTypeRestriction))
                            {
                                yield return new AutocompleteOption(pair.Key, pair.Key.Substring(startText.Length));
                            }
                        } else if (nameValue.GetTypeInfo() == AnalysisToken.AnalysisTokenExtendedTypeInfo)
                        {
                            AnalysisToken analysisValue = (AnalysisToken)nameValue;
                            if (analysisValue.PotentialExecutableDefinition != null)
                            {
                                MetadataTypeRestrictions returnTypeRestriction = analysisValue.PotentialExecutableDefinition.Metadata.ReturnParam.TypeRestrictions;
                                if (typeRestrictions.CanBePassed(returnTypeRestriction))
                                {
                                    yield return new AutocompleteOption(pair.Key, pair.Key.Substring(startText.Length));
                                }
                            }
                        }
                    } else if (searchType == ScopeAnalysisAutocompleteType.All)
                    {
                        throw new ArgumentException("Search type may only be all if type restrictions is all");
                    }
                }
            }
            if (scope.Parent != null)
            {
                foreach (AutocompleteOption parentResult in fromScope(scope.Parent, startText, typeRestrictions, searchType))
                {
                    yield return parentResult;
                }
            }
        }
    }
    public enum ScopeAnalysisAutocompleteType
    {
        MatchingValues,
        MatchingRoutines,
        All
    }
}
