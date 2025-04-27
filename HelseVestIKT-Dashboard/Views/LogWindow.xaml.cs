using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
            // Last loggdata, eksempelvis fra en fil eller en loggvariabel
            LogTextBox.Text = "Loggmeldinger vises her...\n" +
                              "Feil: Kunne ikke koble til VR-headset..., Prøv igjen .\n" +
                              "Warning: Eksempel på warning beskjed.";
        }
    }
}
