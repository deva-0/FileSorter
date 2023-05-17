﻿using Microsoft.Extensions.Options;
using System.IO;

namespace Sorter.Data
{
    public class FileService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<ConfigOptions> _options;
        private List<File> Files;
        private List<Folder> Folders;
        private int indexOfActualProcessingFile;

        private string source;
        private string destination;
        private string[] excludeDirs;
        private string[] whiteList;
        private string[] blackList;
        private bool UseWhiteListInsteadOfBlackList;
        //private int findex2 = 0;
        public FileService(IConfiguration configuration, IOptionsMonitor<ConfigOptions> options)
        {
            _configuration = configuration;
            _options = options;
            try
            {
                source = Path.GetFullPath(_options.CurrentValue.Source);
                destination = Path.GetFullPath(_options.CurrentValue.Destination);
                excludeDirs = Array.ConvertAll(_options.CurrentValue.ExcludeDirs, dir => dir = Path.GetFullPath(dir));
                whiteList = _options.CurrentValue.WhiteList;
                blackList = _options.CurrentValue.BlackList;
                UseWhiteListInsteadOfBlackList = _options.CurrentValue.UseWhiteListInsteadOfBlackList;

            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Probably there is error in config.json");
            }
            catch (Exception)
            {
                throw;
            }
            LoadFilesAndFolders();
            indexOfActualProcessingFile = -1;
        }
        public Task<Folder[]> GetFoldersAsync()
        {
            return Task.FromResult(Folders.ToArray());
        }
        public int? GetNextIndex()
        {
            return (indexOfActualProcessingFile < Files.Count - 1) ? ++indexOfActualProcessingFile : null;
        }
        public File? GetFileAtIndex(int index)
        {
            if (index < Files.Count && index >= 0)
            {
                File file = Files[index];
                file.Path = file.PhysicalPath;
                if (file.Path.Contains(source)) file.Path = string.Concat("/src/", Path.GetRelativePath(source, file.PhysicalPath));
                else if (file.Path.Contains(destination)) file.Path = string.Concat("/dest/", Path.GetRelativePath(destination, file.PhysicalPath));
                file.FIndex = index;
                return file;
            }
            return null;
        }
        public File? GetFileAtIndex(int index, string name)
        {
            File? file = GetFileAtIndex(index);
            if (file == null) return null;
            if (!file.Name.Equals(name)) throw new Exception("You need to reload yor session, the server was reset");
            return file;
        }
        public void ResetFiles()
        {
            try
            {
                source = Path.GetFullPath(_options.CurrentValue.Source);
                destination = Path.GetFullPath(_options.CurrentValue.Destination);
                excludeDirs = Array.ConvertAll(_options.CurrentValue.ExcludeDirs, dir => dir = Path.GetFullPath(dir));
                whiteList = _options.CurrentValue.WhiteList;
                blackList = _options.CurrentValue.BlackList;
                UseWhiteListInsteadOfBlackList = _options.CurrentValue.UseWhiteListInsteadOfBlackList;

            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Probably there is error in config.json");
            }
            catch (Exception)
            {
                throw;
            }
            LoadFilesAndFolders();
            indexOfActualProcessingFile = -1;
        }
        private void LoadFilesAndFolders()
        {
            try
            {
                Files = ProcessFilesInDirectoryRecursively(source);
                Folders = ProcessDirectoryRecursively(destination);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        private List<Folder> ProcessDirectoryRecursively(string targetDirectoryPath)
        {
            List<Folder> folders = new List<Folder>();
            foreach (var fullPathToDirectory in Directory.GetDirectories(targetDirectoryPath))
            {
                try
                {
                    var folderName = Path.GetFileName(fullPathToDirectory);
                    if (!excludeDirs.Contains(fullPathToDirectory))
                    {
                        folders.Add(new Folder(fullPathToDirectory, folderName));
                        folders.AddRange(ProcessDirectoryRecursively(fullPathToDirectory));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error with folder " + fullPathToDirectory + "\n" + e.Message);
                }
            }
            return folders;
        }
        private List<File> ProcessFilesInDirectoryRecursively(string sourceDirectoryPath)
        {
            List<File> files = new List<File>();
            foreach (var fullPathToFile in Directory.GetFiles(sourceDirectoryPath))
            {
                var fileName = Path.GetFileName(fullPathToFile);
                var fileExtension = Path.GetExtension(fullPathToFile).Replace(".", "").ToLower();
                if ((UseWhiteListInsteadOfBlackList && !whiteList.Contains(fileExtension))
                    || (!UseWhiteListInsteadOfBlackList && blackList.Contains(fileExtension))) continue;
                files.Add(new File(fullPathToFile, fileName, fileExtension));
            }
            foreach (var fullPath in Directory.GetDirectories(sourceDirectoryPath))
            {
                try
                {
                    files.AddRange(ProcessFilesInDirectoryRecursively(fullPath));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error with folder " + fullPath + "\n" + e.Message);
                }
            }
            return files;
        }
        public void MoveFile(File file, string destiny)
        {
            try
            {
                //Console.WriteLine(file.OriginalPath + " " + destiny+"\\"+file.Name);
                if (Files[file.FIndex.Value].Name == file.Name)
                {
                    System.IO.File.Move(file.PhysicalPath, Path.Combine(destiny, file.Name));
                    file.PhysicalPath = Path.Combine(destiny, file.Name);
                    Files[file.FIndex.Value].PhysicalPath = file.PhysicalPath;
                }
                else Console.WriteLine("The file index has changed, you need to reload session");
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot move file\n" + e.Message);
            }
        }
        public bool CreateFolder(string folderName)
        {
            if (Folders.Any(f => f.Name == folderName)) return false;
            string fullPath = Path.Combine(destination, folderName);
            Directory.CreateDirectory(fullPath);
            folderName = Path.GetFileName(fullPath);
            if (folderName != null)
            {
                Folders.Add(new Folder(fullPath, folderName));
                return true;
            }
            return false;
        }
        public void ChangeFileName(File file, string newFileName)
        {
            //Console.WriteLine(file.PhysicalPath + "|" + newFileName + "|" + file.Path + "|" + file.Name + "|" + file.FIndex + "|" + file.Extension);
            string newFullPath = Path.Combine(Directory.GetParent(file.PhysicalPath).FullName, newFileName);
            if (!newFullPath.EndsWith("." + file.Extension))
            {
                newFullPath = newFullPath + "." + file.Extension;
            }
            //Console.WriteLine(newFullPath);
            System.IO.File.Move(file.PhysicalPath, newFullPath);
            Files[file.FIndex.Value].PhysicalPath = newFullPath;
            Files[file.FIndex.Value].Name = newFileName;
            Files[file.FIndex.Value].Path = newFullPath;
            if (Files[file.FIndex.Value].Path.Contains(source)) Files[file.FIndex.Value].Path = string.Concat("/src/", Path.GetRelativePath(source, Files[file.FIndex.Value].PhysicalPath));
            else if (Files[file.FIndex.Value].Path.Contains(destination)) Files[file.FIndex.Value].Path = string.Concat("/dest/", Path.GetRelativePath(destination, Files[file.FIndex.Value].PhysicalPath));
            //Console.WriteLine(Files[file.FIndex.Value].PhysicalPath + "|" + newFileName + "|" + Files[file.FIndex.Value].Path + "|" + Files[file.FIndex.Value].Name + "|" + Files[file.FIndex.Value].FIndex + "|" + Files[file.FIndex.Value].Extension);
        }
    }
}
