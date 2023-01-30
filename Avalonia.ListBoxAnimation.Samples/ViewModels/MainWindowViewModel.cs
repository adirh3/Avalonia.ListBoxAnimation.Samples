using System;
using System.Collections.Generic;
using System.Text;

namespace Avalonia.ListBoxAnimation.Samples.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            for (var i = 0; i < 5; i++)
            {
                Tabs.Add("Tab " + i);
            }
            
            for (var i = 0; i < 30; i++)
            {
                Items.Add("Item " + i);
            }
        }

        public List<string> Tabs { get; } = new();
        public List<string> Items { get; } = new();
    }
}