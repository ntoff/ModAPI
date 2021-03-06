﻿/*  
 *  ModAPI
 *  Copyright (C) 2015 FluffyFish / Philipp Mohrenstecher
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *  
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *  
 *  To contact me you can e-mail me at info@fluffyfish.de
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace Updater
{
    class Updater
    {

        public static string rootPath;
        public static string updatePath;

        public static void Update()
        {

            rootPath = Path.GetFullPath(".");
            updatePath = Path.GetFullPath("_update") + Path.DirectorySeparatorChar;
            int c = 100;
            while (c > 0)
            {
                Thread.Sleep(500);
                Process[] ps = Process.GetProcesses();
                bool breakit = true;
                foreach (Process p in ps)
                {
                    try
                    {
                        if (p.ProcessName == "ModAPI")
                            breakit = false;
                    }
                    catch (Exception e)
                    {

                    }
                }
                if (breakit)
                    break;
                c--;
            }

            if (c > 0 && Directory.Exists(updatePath))
            {
                CopyFiles(updatePath);
                Process p = new Process();
                p.StartInfo.FileName = "ModAPI.exe";
                p.StartInfo.Verb = "runas";
                p.Start();
            }
        }

        static void CopyFiles(string directory, string b = "")
        {
            string[] files = Directory.GetFiles(directory);
            foreach (string file in files)
            {
                try
                {
                    File.Copy(file, rootPath + Path.DirectorySeparatorChar + b + Path.GetFileName(file), true);
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    //System.Console.WriteLine(e);
                }
            }
            string[] directories = Directory.GetDirectories(directory);
            foreach (string dir in directories)
            {
                CopyFiles(directory + Path.DirectorySeparatorChar + Path.GetFileName(dir), b + Path.DirectorySeparatorChar + Path.GetFileName(dir) + Path.DirectorySeparatorChar);
                Directory.Delete(dir);
            }
        }
    }
}
