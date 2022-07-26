using OfficeOpenXml;
using System.Data;
using System.IO;

namespace MelBox2
{
    internal class Excel
    {
        // If you use EPPlus in a noncommercial context
        // according to the Polyform Noncommercial license:
        
       // private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>
        /// erzeugt eine Excel-Tabelle aus einer DataTable für den Download
        /// </summary>
        /// <param name="table">DataTable mit Quelldaten</param>
        /// <returns>Excel-Datei als Byte-Array für den Download</returns>
        public static byte[] ConvertToExcel(DataTable table)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            if (table?.TableName.Length < 2) 
                table.TableName = "MelBox2_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH:mm");

            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add(table.TableName);
     
                System.Console.WriteLine($"Excel-Export Tabelle '{table.TableName}'. " +
                    $"Reihen {table.Rows.Count}, " +
                    $"Spalten {table.Columns.Count}");

                for (int c = 0; c < table.Columns.Count; c++)
                {
                    sheet.Cells[1, c + 1].Style.Font.Bold = true;
                    sheet.Cells[1, c + 1].Style.Font.Bold = true;
                    sheet.Cells[1, c + 1].Value = table.Columns[c].ColumnName;

                    for (int r = 0; r < table.Rows.Count; r++)
                    {
                        sheet.Cells[r+2, c+1].Value = table.Rows[r][c].ToString();
                    }

                    sheet.Column(c + 1).AutoFit();
                    sheet.Cells[sheet.Dimension.Address].AutoFilter = true;
                }

                sheet.Calculate();
                package.Workbook.Properties.Title = "Attempts";

                return package.GetAsByteArray();
            }

        }
        //public void CreateExcelFirstTemplate()
        //{
        //    var fileName = "ExcellData.xlsx";
        //    using (var package = new OfficeOpenXml.ExcelPackage(fileName))
        //    {
        //        var worksheet = package.Workbook.Worksheets.FirstOrDefault(x => x.Name == "Attempts");
        //        worksheet = package.Workbook.Worksheets.Add("Assessment Attempts");
        //        worksheet.Row(1).Height = 20;

        //        worksheet.TabColor = Color.Gold;
        //        worksheet.DefaultRowHeight = 12;
        //        worksheet.Row(1).Height = 20;

        //        worksheet.Cells[1, 1].Value = "Employee Number";
        //        worksheet.Cells[1, 2].Value = "Course Code";

        //        var cells = worksheet.Cells["A1:J1"];
        //        var rowCounter = 2;
        //        foreach (var v in userAssessmentsData)
        //        {
        //            worksheet.Cells[rowCounter, 1].Value = v.CompanyNumber;
        //            worksheet.Cells[rowCounter, 2].Value = v.CourseCode;

        //            rowCounter++;
        //        }
        //        worksheet.Column(1).AutoFit();
        //        worksheet.Column(2).AutoFit();


        //        package.Workbook.Properties.Title = "Attempts";
        //        this.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        //        this.Response.AddHeader(
        //                  "content-disposition",
        //                  string.Format("attachment;  filename={0}", "ExcellData.xlsx"));
        //        this.Response.BinaryWrite(package.GetAsByteArray());
        //    }
        //}



    }
}
