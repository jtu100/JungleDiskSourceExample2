/**
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License version 2 as published by
 *  the Free Software Foundation
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *  
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace JungleDisk.JungleDiskGUI
{
    static class Program
    {
        // <junglediskgui>
        //   <accesskey>...</accesskey> 
        //   <secretkey>...</secretkey> 
        // </junglediskgui>
        static string xmlSettingsFilename = "junglediskgui-settings.xml";


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            S3LoginForm loginForm = new S3LoginForm();

            if (File.Exists(xmlSettingsFilename))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(xmlSettingsFilename);
                XmlNode n = null;
                n = doc.SelectSingleNode("//accesskey");
                loginForm.accessKey.Text = n.InnerText;
                n = doc.SelectSingleNode("//secretkey");
                loginForm.secretKey.Text = n.InnerText;
            }

            DialogResult result = loginForm.ShowDialog();
            if (result != DialogResult.OK)
                return;

            JungleDiskConnection connection = new JungleDiskConnection(loginForm.accessKey.Text, loginForm.secretKey.Text);
            connection.BucketExists(new JungleDiskBucket(BucketType.Advanced, "blah", "blah", "blah"));

            Application.Run(new BucketViewForm(connection));
        }
    }
}