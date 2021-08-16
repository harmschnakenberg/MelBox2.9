using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;

namespace MelBox2
{
    partial class Program
    {
        internal static bool CheckDbFile()
        {
            if (!File.Exists(DbPath))
                CreateNewDataBase();

            int numTries = 10;

            while (numTries > 0)
            {
                --numTries;

                try
                {
                    using (FileStream stream = new FileInfo(DbPath).Open(FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        stream.Close();
                        break;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (IOException)
                {
                    //the file is unavailable because it is:
                    //still being written to
                    //or being processed by another thread
                    //or does not exist (has already been processed)
                    Thread.Sleep(200);
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            if (numTries == 0)
              Log.Error("Die Datenbankdatei ist durch ein anderes Programm gesperrt.", 2108121354);

            return numTries > 0;
        }

        internal static bool NonQuery(string query, Dictionary<string, object> args)
        {
            if (!CheckDbFile()) return false;

            try
            {             
                using (var connection = new SqliteConnection("Data Source=" + DbPath))
                {
                    SQLitePCL.Batteries.Init();
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = query;
                    if (args != null && args.Count > 0)
                    {
                        foreach (string key in args.Keys)
                        {
                            command.Parameters.AddWithValue(key, args[key]);
                        }
                    }

                    return command.ExecuteNonQuery() > 0;                   
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                Log.Error("SqlNonQuery(): " + query + "\r\n" + ex.GetType() + "\r\n" + ex.Message, 2108121401);
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        internal static DataTable SelectDataTable(string query, Dictionary<string, object> args)
        {
            if (!CheckDbFile()) return null;

            DataTable myTable = new DataTable();

            try
            {                
                using (var connection = new SqliteConnection("Data Source=" + DbPath))
                {
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = query;
                  
                    if (args != null && args.Count > 0)
                    {
                        foreach (string key in args.Keys)
                        {
                            command.Parameters.AddWithValue(key, args[key]);                        
                        }
                    }

                    try
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            //Mit Schema einlesen
                            myTable.Load(reader);
                        }

                        return myTable;
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch
                    {
#if DEBUG
                        Console.WriteLine("SelectDataTable): Abfrage hat Schema nicht eingehalten."); //Debug-Info
#endif
                        myTable = new DataTable();

                        //Wenn Schema aus DB nicht eingehalten wird (z.B. UNIQUE Constrain in SELECT Abfragen); dann neue DataTable, alle Spalten <string>
                        using (var reader = command.ExecuteReader())
                        {
                            //zu Fuß einlesen
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                //Spalten einrichten
                                myTable.Columns.Add(reader.GetName(i), typeof(string));
                            }

                            while (reader.Read())
                            {
                                List<object> row = new List<object>();

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string colType = myTable.Columns[i].DataType.Name;

                                    if (reader.IsDBNull(i))
                                    {
                                        row.Add(string.Empty);
                                    }
                                    else
                                    {
                                        string r = reader.GetFieldValue<string>(i);
                                        row.Add(r);
                                    }
                                }

                                myTable.Rows.Add(row.ToArray());
                            }
                        }

                        return myTable;
                    }
#pragma warning restore CA1031 // Do not catch general exception types
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                Log.Error("SqlSelectDataTable(): " + query + "\r\n" + ex.GetType() + "\r\n" + ex.Message, 2108121436);
            }
#pragma warning restore CA1031 // Do not catch general exception types

            return myTable;
        }

    }
}
