﻿using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;

namespace MvvmTools.Utilities
{
    internal static class SolutionUtilities
    {
        private static readonly string[] ViewSuffixes = { "View", "Flyout", "UserControl", "Page", "Window", "Dialog" };

        public static List<NamespaceClass> GetClassesInProjectItem(ProjectItem pi)
        {
            var rval = new List<NamespaceClass>();

            if (pi.Name == null)
                return rval;

            if (!pi.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                !pi.Name.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) &&
                !pi.Name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return rval;

            // If has children, that is the source file, check that instead.
            if (pi.ProjectItems != null && pi.ProjectItems.Count != 0)
                foreach (ProjectItem p in pi.ProjectItems)
                    pi = p;

            // If not a part of a project or not compiled, code model will be empty 
            // and there's nothing we can do.
            if (pi?.FileCodeModel == null)
                return rval;

            FileCodeModel fileCm = pi.FileCodeModel;
            CodeElements elts = null;
            elts = fileCm.CodeElements;
            CodeElement elt = null;
            int i = 0;

            var isXaml = pi.Name.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase);
            for (i = 1; i <= fileCm.CodeElements.Count; i++)
            {
                elt = elts.Item(i);
                CollapseElt(rval, elt, elts, i, isXaml);

                // If a xaml.cs code behind file, the first class must be the view type, so we can stop early.
                if (isXaml && rval.Count > 0)
                    break;
            }

            return rval;
        }

        private static void CollapseElt(List<NamespaceClass> classes, CodeElement elt, CodeElements elts, long loc, bool getFirstClassOnly)
        {
            EditPoint epStart = null;
            EditPoint epEnd = null;
            epStart = elt.StartPoint.CreateEditPoint();
            // Do this because we move it later.
            epEnd = elt.EndPoint.CreateEditPoint();
            epStart.EndOfLine();

            var eltKind = elt.Kind;

            if (((elt.IsCodeType) && (elt.Kind == vsCMElement.vsCMElementClass)))
            {
                CodeClass ct = null;
                ct = ((CodeClass)(elt));
                classes.Add(new NamespaceClass(ct.Namespace.Name, ct.Name));

                //CodeElements mems = null;
                //mems = ct.Members;
                //int i = 0;
                //for (i = 1; i <= ct.Members.Count; i++)
                //{
                //    CollapseElt(mems.Item(i), mems, i);
                //}
            }
            else if ((elt.Kind == vsCMElement.vsCMElementNamespace))
            {
                //MessageBox.Show("got a namespace, named: " + elt.Name);
                CodeNamespace cns = null;
                cns = ((CodeNamespace)(elt));
                //MessageBox.Show("set cns = elt, named: " + cns.Name);

                CodeElements mems_vb = null;
                mems_vb = cns.Members;
                //MessageBox.Show("got cns.members");
                int i = 0;

                // Loop through child classes.
                for (i = 1; i <= cns.Members.Count; i++)
                {
                    CollapseElt(classes, mems_vb.Item(i), mems_vb, i, getFirstClassOnly);

                    // If a xaml.cs code behind file, the first class must be the view type, so we can stop early.
                    if (getFirstClassOnly && classes.Count > 0)
                        return;
                }
            }
        }

        public static List<ProjectItemAndType> GetRelatedDocuments(ProjectItem pi, IEnumerable<string> typeNamesInFile)
        {
            var rval = new List<ProjectItemAndType>();

            var candidateTypeNames = GetTypeCandidates(typeNamesInFile);

            // Look for the candidate types in current project first, excluding the selected project item.
            var documents = FindDocumentsContainingTypes(pi.ContainingProject, pi, candidateTypeNames);

            // Then add candidates from the rest of the solution.
            var solution = pi.DTE?.Solution;
            if (solution != null)
            {
                foreach (Project project in solution.Projects)
                {
                    if (project == pi.ContainingProject)
                        continue;

                    var docs = FindDocumentsContainingTypes(project, pi, candidateTypeNames);
                    documents.AddRange(docs);
                }
            }

            rval = documents.Distinct(new ProjectItemAndTypeEqualityComparer()).ToList();

            return rval;
        }

        public static List<string> GetTypeCandidates(IEnumerable<string> typeNamesInFile)
        {
            var candidates = new List<string>();

            // For each type name in the file, create a list of candidates.
            foreach (var typeName in typeNamesInFile)
            {
                // If a view model...
                if (typeName.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove ViewModel from end and add all the possible suffixes.
                    var baseName = typeName.Substring(0, typeName.Length - 9);
                    foreach (var suffix in ViewSuffixes)
                    {
                        var candidate = baseName + suffix;
                        candidates.Add(candidate);
                    }

                    // Add base if it ends in one of the view suffixes.
                    foreach (var suffix in ViewSuffixes)
                        if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(baseName);
                            break;
                        }
                }

                foreach (var suffix in ViewSuffixes)
                {
                    if (typeName.EndsWith(suffix))
                    {
                        // Remove suffix and add ViewModel.
                        var baseName = typeName.Substring(0, typeName.Length - suffix.Length);
                        var candidate = baseName + "ViewModel";
                        candidates.Add(candidate);

                        // Just add ViewModel
                        candidate = typeName + "ViewModel";
                        candidates.Add(candidate);
                    }
                }
            }

            return candidates;
        }

        public static List<ProjectItemAndType> FindDocumentsContainingTypes(Project project, ProjectItem excludeProjectItem, List<string> typesToFind)
        {
            var results = new List<ProjectItemAndType>();

            FindDocumentsContainingTypesRecursive(excludeProjectItem, project.ProjectItems, typesToFind, null, results);

            return results;
        }

        private static void FindDocumentsContainingTypesRecursive(ProjectItem excludeProjectItem, ProjectItems projectItems, List<string> typesToFind, ProjectItem parentProjectItem, List<ProjectItemAndType> results)
        {
            if (typesToFind.Count == 0 || projectItems == null)
                return;

            var tmpResults = new List<ProjectItemAndType>();

            foreach (ProjectItem pi in projectItems)
            {
                // Exclude the document we're on.
                if (pi == excludeProjectItem)
                    continue;

                // Recursive call
                if (pi.Name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                    FindDocumentsContainingTypesRecursive(excludeProjectItem, pi.ProjectItems, typesToFind, pi, tmpResults);
                else
                    FindDocumentsContainingTypesRecursive(excludeProjectItem, pi.ProjectItems ?? pi.SubProject?.ProjectItems, typesToFind,
                        null, tmpResults);

                // Only search source files.
                if (!pi.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                    !pi.Name.EndsWith(".vb", StringComparison.OrdinalIgnoreCase))
                    continue;

                var classesInProjectItem = GetClassesInProjectItem(pi);

                var xamlSaved = false;
                foreach (var c in classesInProjectItem)
                {
                    if (typesToFind.Contains(c.Class, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!xamlSaved && parentProjectItem != null)
                        {
                            // Parent is the xaml file corresponding to this xaml.cs.  We save it once.
                            tmpResults.Add(new ProjectItemAndType(parentProjectItem, c));
                            xamlSaved = true;
                        }

                        tmpResults.Add(new ProjectItemAndType(pi, c));
                    }
                }
            }
            
            results.AddRange(tmpResults);
        }

        private class ProjectItemAndTypeEqualityComparer : IEqualityComparer<ProjectItemAndType>
        {
            public bool Equals(ProjectItemAndType x, ProjectItemAndType y)
            {
                return x.ProjectItem.Name == y.ProjectItem.Name &&
                       x.Type == y.Type;
            }

            public int GetHashCode(ProjectItemAndType obj)
            {
                return (obj.ProjectItem.Name + ";" + obj.Type).GetHashCode();
            }
        }
    }

    public class ProjectItemAndType
    {
        public ProjectItemAndType(ProjectItem projectItem, NamespaceClass fullType)
        {
            ProjectItem = projectItem;
            Type = fullType;
        }

        public ProjectItem ProjectItem { get; set; }
        public NamespaceClass Type { get; set; }

        public string RelativeNamespace
        {
            get
            {
                if (Type.Namespace.StartsWith(ProjectItem.ContainingProject.Name))
                    return Type.Namespace.Substring(ProjectItem.ContainingProject.Name.Length);
                else
                    return Type.Namespace;
            }
        }
    }

    public class NamespaceClass
    {
        public NamespaceClass(string @namespace, string @class)
        {
            Namespace = @namespace;
            Class = @class;
        }

        public string Namespace { get; set; }
        public string Class { get; set; }
    }
}
