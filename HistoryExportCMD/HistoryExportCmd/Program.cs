using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HistoryExportCmd;

internal class Program : IDisposable
{
	private LogFile mLogfile;

	private static int Main(string[] args)
	{
		int num = 0;
		using Program program = new Program();
		return program.DoWork();
	}

	private Program()
	{
		Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
		NameValueCollection appSettings = ConfigurationManager.AppSettings;
		string text = appSettings["LogPath"];
		if (text != null)
		{
			Directory.CreateDirectory(text);
		}
		LogFile.SetConfig(typeof(LogFlags), appSettings);
		mLogfile = new LogFile("HistoryExportCmd_#.log");
		mLogfile.Write(LogFlags.TzINFORMATION, "Starting");
	}

	public void Dispose()
	{
		mLogfile.Write(LogFlags.TzINFORMATION, "Finished\n\n");
	}

	private int DoWork()
	{
		string connectionString = ConfigurationManager.ConnectionStrings["PointsHistory"].ConnectionString;
		string connectionString2 = ConfigurationManager.ConnectionStrings["EBI_ODBC"].ConnectionString;
		string connectionString3 = ConfigurationManager.ConnectionStrings["EBI_SQL"].ConnectionString;
		DBAccess dBAccess = new DBAccess(mLogfile, connectionString3, connectionString2, connectionString);
		bool primary;
		bool eBIStatus = dBAccess.GetEBIStatus(out primary);
		if (eBIStatus)
		{
			if (primary)
			{
				Process(dBAccess);
			}
			else
			{
				Synchronize();
			}
		}
		else
		{
			mLogfile.Write(LogFlags.TzINFORMATION, "Failure reading the EBI status, the process cannot run");
		}
		if (!eBIStatus)
		{
			return 1;
		}
		return 0;
	}

	private void Process(DBAccess dbaccess)
	{
		mLogfile.Write(LogFlags.TzINFORMATION, "Starting the process");
		DateTime dateTime = DateTime.Now.ToUniversalTime();
		int num = Convert.ToInt32(ConfigurationManager.AppSettings["OldestDayFromToday"]);
		DateTime dateTime2 = dateTime.Date.AddDays(-num);
		if (dbaccess.GetPoints(out var points))
		{
			mLogfile.Write(LogFlags.TzINFORMATION, "{0} points read from database", points.Count);
			for (int i = 1; i <= 3; i++)
			{
				mLogfile.Write(LogFlags.TzINFORMATION, "Working on {0}", i switch
				{
					2 => "Standard History", 
					1 => "Fast History", 
					_ => "Extended History", 
				});
				List<Point> list = null;
				if (i == 1)
				{
					list = points.Where((Point p) => p.HistoryFast && p.HistoryFastArch).ToList();
				}
				if (i == 2)
				{
					list = points.Where((Point p) => p.HistorySlow && p.HistorySlowArch).ToList();
				}
				if (i == 3)
				{
					list = points.Where((Point p) => p.HistoryExtd && p.HistoryExtdArch).ToList();
				}
				mLogfile.Write(LogFlags.TzINFORMATION, "{0} points configured for this History type", list.Count);
				if (list.Count <= 0)
				{
					continue;
				}
				int num2 = 0;
				if (i == 1)
				{
					num2 = 5;
				}
				if (i == 2)
				{
					num2 = 60;
				}
				if (i == 3)
				{
					num2 = 3600;
				}
				DateTime lastDatetime;
				bool flag = dbaccess.GetLastDatetime(i, out lastDatetime);
				if (!flag)
				{
					continue;
				}
				lastDatetime = ((lastDatetime < dateTime2) ? dateTime2 : lastDatetime.AddSeconds(3.0));
				DateTime dateTime3 = dateTime.AddMinutes(-130.0);
				while (lastDatetime < dateTime3 && flag)
				{
					DateTime dateTime4 = lastDatetime.AddSeconds(3200 * num2);
					if (dateTime4 > dateTime3)
					{
						dateTime4 = dateTime3;
					}
					mLogfile.Write(LogFlags.TzINFORMATION, "iniDateTime: {0}", lastDatetime);
					mLogfile.Write(LogFlags.TzINFORMATION, "endDateTime: {0}", dateTime4);
					if (dateTime4 - lastDatetime > TimeSpan.FromSeconds(num2))
					{
						flag = dbaccess.Prepare();
						if (flag)
						{
							int num3 = 10;
							for (int num4 = 0; num4 < list.Count && flag; num4 += num3)
							{
								List<Point> list2 = new List<Point>();
								for (int num5 = 0; num5 < num3 && num4 + num5 < list.Count; num5++)
								{
									list2.Add(list[num4 + num5]);
								}
								flag = dbaccess.GetHistory(i, lastDatetime, dateTime4, list2, out var lhistory);
								if (flag)
								{
									flag = dbaccess.StoreHistory(i, lhistory);
								}
							}
							if (flag)
							{
								flag = dbaccess.Finish(i);
							}
						}
					}
					lastDatetime = dateTime4;
				}
			}
		}
		mLogfile.Write(LogFlags.TzINFORMATION, "Process finished");
	}

	private void Synchronize()
	{
		if (Convert.ToBoolean(ConfigurationManager.AppSettings["RedundantPointHistory"]))
		{
			string machineName = Environment.MachineName;
			string text = "";
			text = ((!machineName.EndsWith("A")) ? (machineName.Substring(0, machineName.Length - 1) + "A") : (machineName.Substring(0, machineName.Length - 1) + "B"));
			mLogfile.Write(LogFlags.TzINFORMATION, "Starting the process");
			string connectionString = ConfigurationManager.ConnectionStrings["PointsHistory"].ConnectionString;
			DBSync dBSync = new DBSync(mLogfile, connectionString);
			bool flag = dBSync.SyncPoint(text, machineName);
			for (int i = 1; i <= 3 && flag; i++)
			{
				flag = dBSync.SyncHistoryTable(text, machineName, i);
			}
			mLogfile.Write(LogFlags.TzINFORMATION, "Process finished");
		}
	}
}
