using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace GstTool.ViewModel
{
    public class FastViewModel : ObservableObject
    {
        private static FastViewModel _instance;

        public static FastViewModel CreateInstance()
        {
            _instance ??= new FastViewModel();
            return _instance;
        }
    }
}