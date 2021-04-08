using System.Collections.Generic;
using MvvmTools.Services;

namespace MvvmTools.Models
{
    /// <summary>
    /// This class contains the entire Mvvm Tools configuration settings.
    /// </summary>
    public class MvvmToolsSettings
    {
        public MvvmToolsSettings()
        {
            // Set default values.
            GoToViewOrViewModelOption = GoToViewOrViewModelOption.ShowUi;
            ProjectOptions = new List<ProjectOptions>();

            Prefixes = SettingsService.DefaultViewSuffixes;
            ViewSuffixes = SettingsService.DefaultViewSuffixes;
        }

        public GoToViewOrViewModelOption GoToViewOrViewModelOption { get; set; }
        public bool GoToViewOrViewModelSearchSolution { get; set; }

        public string[] Prefixes { get; set; }
        public string[] ViewSuffixes { get; set; }
        
        // Configuration settings for the solutions.
        public ProjectOptions SolutionOptions { get; set; }

        // Where the user's local templates are stored.
        public string LocalTemplateFolder { get; set; }

        // Contains the list of configuration settings for the projects.
        public IList<ProjectOptions> ProjectOptions { get; set; }
    }
}