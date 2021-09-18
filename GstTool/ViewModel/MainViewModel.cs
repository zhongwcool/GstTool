using System;
using GstTool.Model;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;

namespace GstTool.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private static MainViewModel _instance;

        public RelayCommand<string> CommandPlayStream => new Lazy<RelayCommand<string>>(
            () => new RelayCommand<string>(OnPlayStream)
        ).Value;

        public static MainViewModel CreateInstance()
        {
            _instance ??= new MainViewModel();
            return _instance;
        }

        private static void OnPlayStream(string msg)
        {
            WeakReferenceMessenger.Default.Send(new Message(Message.PlayStream));
        }
    }
}