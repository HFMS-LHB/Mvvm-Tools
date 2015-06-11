﻿//------------------------------------------------------------------------------
// <copyright file="GoToViewOrViewModelCommand.cs" company="Chris Bordeman">
//     Copyright (c) 2015 Chris Bordeman.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MvvmTools.Services;
using MvvmTools.ViewModels;
using MvvmTools.Views;
using Ninject;

// ReSharper disable HeapView.BoxingAllocation

namespace MvvmTools.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class GoToViewOrViewModelCommand : BaseCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoToViewOrViewModelCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        public GoToViewOrViewModelCommand()
            : base(Constants.GoToViewOrViewModelCommandId)
        {
        }

        [Inject]
        public ISolutionService SolutionService { get; set; }

        protected override void OnExecute()
        {
            base.OnExecute();

            if (Package.ActiveDocument?.ProjectItem != null)
            {
                var classesInFile = SolutionService.GetClassesInProjectItem(Package.ActiveDocument.ProjectItem);

                if (classesInFile.Count == 0)
                {
                    MessageBox.Show("No classes found in file.", "MVVM Tools");
                    return;
                }

                var docs = SolutionService.GetRelatedDocuments(Package.ActiveDocument.ProjectItem, classesInFile.Select(c => c.Class));

                if (docs.Count == 0)
                {
                    string classes = "\n        ";
                    foreach (var c in classesInFile)
                        classes += c.Class + "\n        ";

                    MessageBox.Show(string.Format("Couldn't find any matching view or view model classes.\n\nClasses found in this file ({0}):\n{1}",
                        Package.ActiveDocument.FullName,
                        classes), "MVVM Tools");

                    return;
                }

                var settings = SettingsService.LoadSettings();
                if (docs.Count == 1 || settings.GoToViewOrViewModelOption == GoToViewOrViewModelOption.ChooseFirst)
                {
                    var win = docs[0].ProjectItem.Open();
                    win.Visible = true;
                    win.Activate();

                    return;
                }

                // Multiple results.
                if (settings.GoToViewOrViewModelOption == GoToViewOrViewModelOption.ShowUi)
                {
                    PresentViewViewModelOptions(docs);
                    return;
                }

                // If there are more than one .xaml files or there are more than one code
                // behind files, then we must show the UI.
                var countXaml = docs.Count(d => d.ProjectItem.Name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));
                if (countXaml > 1)
                {
                    PresentViewViewModelOptions(docs);
                    return;
                }
                var countCodeBehind = docs.Count(d => d.ProjectItem.Name.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase) ||
                                                      d.ProjectItem.Name.EndsWith(".xaml.vb", StringComparison.OrdinalIgnoreCase));
                if (countCodeBehind > 1)
                {
                    PresentViewViewModelOptions(docs);
                    return;
                }

                // If the count of files is > 2 now, then we must show UI.
                var count = docs.Count;
                if (count > 2)
                {
                    PresentViewViewModelOptions(docs);
                    return;
                }

                // If the remaining two files are xaml and code behind, we can apply the 
                // 'prefer xaml' or 'prefer code behind' setting.
                if (String.Compare(docs[0].ProjectItem.Name, docs[1].ProjectItem.Name + ".cs", StringComparison.OrdinalIgnoreCase) == 0 ||
                    String.Compare(docs[0].ProjectItem.Name, docs[1].ProjectItem.Name + ".vb", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // First file is code behind, second is XAML.
                    if (settings.GoToViewOrViewModelOption == GoToViewOrViewModelOption.ChooseCodeBehind)
                    {
                        var win = docs[0].ProjectItem.Open();
                        win.Visible = true;
                        win.Activate();
                    }
                    else
                    {
                        var win = docs[1].ProjectItem.Open();
                        win.Visible = true;
                        win.Activate();
                    }
                }
                else if (String.Compare(docs[1].ProjectItem.Name, docs[0].ProjectItem.Name + ".cs", StringComparison.OrdinalIgnoreCase) == 0 ||
                    String.Compare(docs[1].ProjectItem.Name, docs[0].ProjectItem.Name + ".vb", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // First file is XAML, second is code behind.
                    if (settings.GoToViewOrViewModelOption == GoToViewOrViewModelOption.ChooseXaml)
                    {
                        var win = docs[0].ProjectItem.Open();
                        win.Visible = true;
                        win.Activate();
                    }
                    else
                    {
                        var win = docs[1].ProjectItem.Open();
                        win.Visible = true;
                        win.Activate();
                    }
                }
                else
                {
                    // The two files are unrelated, must show UI.
                    PresentViewViewModelOptions(docs);
                }
            }
        }

        private void PresentViewViewModelOptions(List<ProjectItemAndType> docs)
        {
            var window = new SelectFileDialog();
            var vm = new SelectFileDialogViewModel(docs);
            window.DataContext = vm;

            var result = window.ShowDialog();

            if (result.GetValueOrDefault())
            {
                // Go to the selected project item.
                var win = vm.SelectedDocument.ProjectItem.Open();
                win.Visible = true;
                win.Activate();
            }
        }
    }
}
