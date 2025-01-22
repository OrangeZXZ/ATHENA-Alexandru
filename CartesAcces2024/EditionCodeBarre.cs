﻿using Word = Microsoft.Office.Interop.Word;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text;
using System.ComponentModel;

namespace CartesAcces2024
{
    public class EditionCodeBarre
    {
        private static List<ACTIVEBARCODELib.Barcode> codesBarresGeneres = new List<ACTIVEBARCODELib.Barcode>();
        private static List<string> cheminsImagesTemp = new List<string>();

        public static void GenererCodesBarres(List<string> codes, BackgroundWorker worker)
        {
            codesBarresGeneres.Clear();
            cheminsImagesTemp.Clear();
            int total = codes.Count;
            int progress = 0;

            foreach (string code in codes)
            {
                if (worker.CancellationPending)
                {
                    return;
                }

                try
                {
                    var barcodeControl = new ACTIVEBARCODELib.Barcode();
                    ConfigureBarcode(barcodeControl, code);
                    codesBarresGeneres.Add(barcodeControl);

                    // Générer et sauvegarder l'image temporaire
                    string tempImagePath = System.IO.Path.GetTempFileName() + ".png";
                    barcodeControl.SaveAsBySize(tempImagePath, 356, 120);
                    cheminsImagesTemp.Add(tempImagePath);

                    progress++;
                    worker.ReportProgress((int)((double)progress / total * 100));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de la génération du code-barre: {ex.Message}");
                }
            }
        }

        public static void SauvegarderPdfCodeBarres(string cheminDestination, BackgroundWorker worker)
        {
            Word.Application wordApp = null;
            Word.Document doc = null;

            try
            {
                wordApp = new Word.Application { Visible = false };
                doc = wordApp.Documents.Add();

                ConfigurePageMargins(doc, wordApp);

                int rows = (int)Math.Ceiling(cheminsImagesTemp.Count / 3.0);
                Word.Table table = doc.Tables.Add(doc.Range(0, 0), rows, 3);
                table.Borders.Enable = 0;

                int total = cheminsImagesTemp.Count;
                for (int i = 0; i < total; i++)
                {
                    if (worker.CancellationPending)
                    {
                        return;
                    }

                    int row = (i / 3) + 1;
                    int col = (i % 3) + 1;

                    Word.Cell cell = table.Cell(row, col);
                    Word.Range cellRange = cell.Range;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                    InsertBarcodeImage(cellRange, cheminsImagesTemp[i]);

                    worker.ReportProgress((int)((double)(i + 1) / total * 100));
                }

                doc.SaveAs2(cheminDestination, Word.WdSaveFormat.wdFormatPDF);
            }
            finally
            {
                CleanUpResources(doc, wordApp);
                // Nettoyer les fichiers temporaires
                foreach (string tempFile in cheminsImagesTemp)
                {
                    if (System.IO.File.Exists(tempFile))
                    {
                        try
                        {
                            System.IO.File.Delete(tempFile);
                        }
                        catch { }
                    }
                }
                cheminsImagesTemp.Clear();
                
                // Libérer les ressources des codes-barres
                foreach (var barcode in codesBarresGeneres)
                {
                    try
                    {
                        Marshal.ReleaseComObject(barcode);
                    }
                    catch { }
                }
                codesBarresGeneres.Clear();
            }
        }

        private static void ConfigurePageMargins(Word.Document doc, Word.Application wordApp)
        {
            float margin = wordApp.CentimetersToPoints(1.5f);
            doc.PageSetup.TopMargin = margin;
            doc.PageSetup.BottomMargin = margin;
            doc.PageSetup.LeftMargin = margin;
            doc.PageSetup.RightMargin = margin;
        }

        private static void InsertBarcodesIntoTable(List<string> codesBarres, Word.Table table)
        {
            int currentRow = 1;
            int currentCol = 1;

            foreach (string code in codesBarres)
            {
                Word.Cell cell = table.Cell(currentRow, currentCol);
                Word.Range cellRange = cell.Range;

                try
                {
                    string cleanCode = RemoveAccents(code);
                    InsererCodeBarre(cleanCode, cellRange);

                    // Mise à jour des positions dans la table
                    if (currentCol == 2)
                    {
                        currentCol = 1;
                        currentRow++;
                    }
                    else
                    {
                        currentCol++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de la génération du code-barre: {ex.Message}");
                    continue;
                }
            }
        }

        private static void CleanUpResources(Word.Document doc, Word.Application wordApp)
        {
            if (doc != null)
            {
                doc.Close(false);
                Marshal.ReleaseComObject(doc);
            }
            if (wordApp != null)
            {
                wordApp.Quit();
                Marshal.ReleaseComObject(wordApp);
            }
        }

        private static void InsererCodeBarre(string code, Word.Range cellRange)
        {
            ACTIVEBARCODELib.Barcode barcodeControl = new ACTIVEBARCODELib.Barcode();
            try
            {
                // Configuration du code-barre
                ConfigureBarcode(barcodeControl, code);

                // Générer et sauvegarder temporairement l'image
                string tempImagePath = System.IO.Path.GetTempFileName() + ".png";
                barcodeControl.SaveAsBySize(tempImagePath, 356, 120);

                if (!System.IO.File.Exists(tempImagePath))
                {
                    throw new Exception("Échec de la création du fichier image temporaire");
                }

                try
                {
                    // Centrer le paragraphe avant d'insérer l'image
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    
                    // Insérer l'image dans Word
                    InsertBarcodeImage(cellRange, tempImagePath);
                }
                finally
                {
                    // Nettoyer le fichier temporaire
                    if (System.IO.File.Exists(tempImagePath))
                    {
                        System.IO.File.Delete(tempImagePath);
                    }
                }
            }
            finally
            {
                // Libérer les ressources COM
                if (barcodeControl != null)
                {
                    Marshal.ReleaseComObject(barcodeControl);
                }
            }
        }

        private static void ConfigureBarcode(ACTIVEBARCODELib.Barcode barcodeControl, string code)
        {
            barcodeControl.Type = ACTIVEBARCODELib.TypeConstants.CODECODE128;
            barcodeControl.ShowText = true;
            barcodeControl.AutoType = false;
            barcodeControl.Text = code;
        }

        private static void InsertBarcodeImage(Word.Range cellRange, string tempImagePath)
        {
            Word.InlineShape shape = cellRange.InlineShapes.AddPicture(tempImagePath);
            shape.Width = 160;
            shape.Height = 60;

            cellRange.InsertParagraphAfter();
            cellRange.Paragraphs[1].SpaceAfter = 0;
            cellRange.Paragraphs[1].SpaceBefore = 30;

            cellRange.Cells[1].LeftPadding = 15;
            cellRange.Cells[1].RightPadding = 15;
        }

        private static string RemoveAccents(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            StringBuilder normalizedString = new StringBuilder();
            foreach (char c in input)
            {
                switch (c)
                {
                    case 'É': normalizedString.Append('E'); break;
                    case 'È': normalizedString.Append('E'); break;
                    case 'Ê': normalizedString.Append('E'); break;
                    case 'Ë': normalizedString.Append('E'); break;
                    case 'À': normalizedString.Append('A'); break;
                    case 'Â': normalizedString.Append('A'); break;
                    case 'Ä': normalizedString.Append('A'); break;
                    case 'Î': normalizedString.Append('I'); break;
                    case 'Ï': normalizedString.Append('I'); break;
                    case 'Ô': normalizedString.Append('O'); break;
                    case 'Ö': normalizedString.Append('O'); break;
                    case 'Ù': normalizedString.Append('U'); break;
                    case 'Û': normalizedString.Append('U'); break;
                    case 'Ü': normalizedString.Append('U'); break;
                    case 'Ç': normalizedString.Append('C'); break;

                    case 'é': normalizedString.Append('e'); break;
                    case 'è': normalizedString.Append('e'); break;
                    case 'ê': normalizedString.Append('e'); break;
                    case 'ë': normalizedString.Append('e'); break;
                    case 'à': normalizedString.Append('a'); break;
                    case 'â': normalizedString.Append('a'); break;
                    case 'ä': normalizedString.Append('a'); break;
                    case 'î': normalizedString.Append('i'); break;
                    case 'ï': normalizedString.Append('i'); break;
                    case 'ô': normalizedString.Append('o'); break;
                    case 'ö': normalizedString.Append('o'); break;
                    case 'ù': normalizedString.Append('u'); break;
                    case 'û': normalizedString.Append('u'); break;
                    case 'ü': normalizedString.Append('u'); break;
                    case 'ç': normalizedString.Append('c'); break;
                    
                    default: normalizedString.Append(c); break;
                }
            }
            return normalizedString.ToString();
        }
    }
}