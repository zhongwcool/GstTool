﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GstTool.Annotations;
using GstTool.Model;

namespace GstTool.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
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
            Messenger.Default.Send(new Message(Message.Main.PlayStream), Message.Token.Main);
        }


        #region 继承接口

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}