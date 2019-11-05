// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using Xunit;

namespace Microsoft.VisualStudio.Web.CodeGeneration.Core.FunctionalTest
{
    public class NupkgTest
    {
        [Fact]
        public void CheckFolderStructure()
        {
            string nuget300Package = "https://www.nuget.org/api/v2/package/Microsoft.VisualStudio.Web.CodeGeneration.Design/3.0.0";
            string nugetOrgPkg = "Microsoft.VisualStudio.Web.CodeGeneration.Design.3.0.0.nupkg";
            string artifactsPath = "../../../../packages/Debug/Shipping/";
            DirectoryInfo taskDirectory = new DirectoryInfo(artifactsPath);
            string buildPkg = "Microsoft.VisualStudio.Web.CodeGeneration.Design*.nupkg";
            string fileToUse = "";

            foreach (var file in taskDirectory.GetFiles(buildPkg))
            {
                if (!file.Name.Contains("symbols"))
                {
                    fileToUse = artifactsPath + file.Name;
                }
            }

            Assert.False(string.IsNullOrEmpty(fileToUse));
            using (WebClient myWebClient = new WebClient())
            {
                myWebClient.DownloadFile(nuget300Package, fileToUse);
            }

            ZipArchive zipOne, zipTwo;
            Dictionary<string, string> nuget300files = new Dictionary<string, string>();

            
            using(zipOne = ZipFile.OpenRead(fileToUse))
            {
                foreach (ZipArchiveEntry entry in zipOne.Entries)
                {
                    //use full path name as key due to duplicate exes, xmls.
                    nuget300files.Add(entry.FullName, entry.Name);
                }
            }
            using (zipTwo = ZipFile.OpenRead(nugetOrgPkg))
            {
                foreach (ZipArchiveEntry entry in zipTwo.Entries)
                {
                    //make sure new pkg has atleast all the old pkg files
                    Assert.True(nuget300files.ContainsKey(entry.FullName));
                    nuget300files.TryGetValue(entry.FullName, out string nuget300value);
                    Assert.Equal(entry.Name, nuget300value);
                }
                
            }

        }
    }
}
