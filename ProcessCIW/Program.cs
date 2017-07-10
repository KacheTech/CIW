﻿using Gsa.Sftp.Libraries.Utilities.Encryption;
using ProcessCIW.Models;
using ProcessCIW.Validation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;

namespace ProcessCIW
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Stopwatch stopWatch = new Stopwatch();

        private static ProcessDocuments pd = new ProcessDocuments();

        //Need better naming namespace and convention here
        private static Utilities.Utilities u = new Utilities.Utilities();

        static void Main(string[] args)
        {
            stopWatch.Start();

            log.Info("Application Started");
           
            //Need to get data from the DB for the uplaoder ID
            ProcessFiles();

            log.Info(string.Format("Processed Adjudications in {0} milliseconds", stopWatch.ElapsedMilliseconds));

            stopWatch.Stop();

            log.Info("Application Done");

            Console.WriteLine("Done! " + stopWatch.ElapsedMilliseconds);
                
            return;
        }

        private static void ProcessFiles()
        {
            List<UnprocessedFiles> uf = new List<UnprocessedFiles>();

			log.Info(string.Format("Getting unprocessed files"));
            uf = pd.GetUnprocessedFiles();

            foreach (string oldCSVFiles in Directory.EnumerateFiles(ConfigurationManager.AppSettings["TEMPFOLDER"], "*.csv"))
            {
                try
                {
                    log.Info(string.Format("Deleting old CSV file (0).", oldCSVFiles));
                    File.Delete(oldCSVFiles);
                }
                catch (IOException e)
                {
                    log.Error(e.Message + " - " + e.InnerException);
                }
            }

            if (uf.Count == 0)
            {
                log.Info("No Files Found For Processing");
                return;
            }
            else
                log.Info(string.Format("Found {0} unprocessed files.", uf.Count));

            if (ConfigurationManager.AppSettings["DEBUGMODE"] == "true")
            {
                log.Info("Processing Debug Files");
                ProcessDebugFiles(uf);
            }
            else
            {
                log.Info("Processing Prod Files");
                ProcessProdFiles(uf);
            }

            return;
        }

        private static void ProcessDebugFiles(List<UnprocessedFiles> filesForProcessing)
        {
            int processedResult;

            foreach (var ciwFile in filesForProcessing)
            {
				List<CIWData> dupes = new List<CIWData>();
                string filePath = ConfigurationManager.AppSettings["CIWDEBUGFILELOCATION"] + ciwFile.FileName;
                
                log.Info(string.Format("Processing file {0}", filePath));

                string tempFile = pd.GetCIWInformation(ciwFile.PersID, filePath, ciwFile.FileName, out dupes);

                if (tempFile != null)
                {
                    log.Info(string.Format("GetCIWInformation returned with temp file {0} and had {1} nested field(s).", tempFile, dupes.Count));

                    processedResult = pd.ProcessCIWInformation(ciwFile.PersID, tempFile, true, dupes);

                    log.Info(string.Format("ProcessCIWInformation returned with result: {0}", processedResult == 1 ? "File processed successfully" : processedResult == 0 ? "File remains unprocessed" : "File failed processing"));

                    pd.UpdateProcessed(ciwFile.ID, processedResult);
                }
                else
                {
                    pd.UpdateProcessed(ciwFile.ID, -1);
                }

                try
                {
                    Utilities.Utilities.DeleteFiles(new List<string> { filePath });
                }
                catch (IOException e)
                {
                    log.Error(e.Message + " - " + e.InnerException);
                }
            }
        }

        private static void ProcessProdFiles(List<UnprocessedFiles> filesForProcessing)
        {
            int processedResult;

            foreach (var ciwFile in filesForProcessing)
            {
				List<CIWData> dupes;
                string filePath = ConfigurationManager.AppSettings["CIWPRODUCTIONFILELOCATION"] + ciwFile.FileName;

                log.Info(string.Format("Processing file {0}", filePath));

                byte[] buffer = new byte[] { };

                string decryptedFile = string.Empty;

                decryptedFile = ConfigurationManager.AppSettings["CIWPRODUCTIONFILELOCATION"] + u.GenerateDecryptedFilename(Path.GetFileNameWithoutExtension(ciwFile.FileName));

                log.Info(string.Format("Decrypting file {0}.", ciwFile));

                buffer = File.ReadAllBytes(filePath);

                buffer.WriteToFile(decryptedFile, Cryptography.Security.Decrypt, true);

                string tempFile = pd.GetCIWInformation(ciwFile.PersID, decryptedFile, ciwFile.FileName, out dupes);

                if (tempFile != null)
                {

                    log.Info(string.Format("GetCIWInformation returned with temp file {0} and had {1} nested field(s).", tempFile, dupes.Count));

                    processedResult = pd.ProcessCIWInformation(ciwFile.PersID, tempFile, true, dupes);

                    log.Info(string.Format("ProcessCIWInformation returned with result: {0}", processedResult == 1 ? "File processed successfully" : processedResult == 0 ? "File remains unprocessed" : "File failed processing"));

                    pd.UpdateProcessed(ciwFile.ID, processedResult);
                }
                else
                    pd.UpdateProcessed(ciwFile.ID, -1);

                try
                {
                    Utilities.Utilities.DeleteFiles(new List<string> { filePath, decryptedFile });
                }
                catch (IOException e)
                {
                    log.Error(e.Message + " - " + e.InnerException);
                }
            }
        }
    }
}