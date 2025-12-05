using System;
using System.Data;
using System.Data.SqlClient;

namespace HistoryExportCmd;

internal class DBSync
{
	private string mCnPHistory;

	private LogFile mLogFile;

	public DBSync(LogFile logFile, string cnPointsHistory)
	{
		mCnPHistory = cnPointsHistory;
		mLogFile = logFile;
	}

	public bool SyncPoint(string pServer, string bServer)
	{
		bool result = false;
		try
		{
			mLogFile.Write(LogFlags.TzINFORMATION, "Synchronizing Point table");
			using (SqlConnection sqlConnection = new SqlConnection(mCnPHistory))
			{
				sqlConnection.Open();
				using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
				{
					sqlCommand.CommandText = string.Format("insert into {1}.PointsHistory.dbo.Point select P.* from {0}.PointsHistory.dbo.Point P left join {1}.PointsHistory.dbo.Point B on P.PointId = B.PointId where b.PointId is null", pServer, bServer);
					mLogFile.Write(LogFlags.TzSQL, sqlCommand);
					int num = sqlCommand.ExecuteNonQuery();
					mLogFile.Write(LogFlags.TzINFORMATION, "{0} records inserted in backup server", num);
				}
				using SqlCommand sqlCommand2 = sqlConnection.CreateCommand();
				sqlCommand2.CommandText = $"update B set B.PointName = P.PointName, B.ParamName = P.ParamName, B.Description = P.Description, B.Device = P.Device, B.HistoryFast = P.HistoryFast, B.HistorySlow = P.HistorySlow, B.HistoryExtd = P.HistoryExtd, B.HistoryFastArch = P.HistoryFastArch, B.HistorySlowArch = P.HistorySlowArch, B.HistoryExtdArch = P.HistoryExtdArch from {pServer}.PointsHistory.dbo.Point P join {bServer}.PointsHistory.dbo.Point B on P.PointId = B.PointId";
				mLogFile.Write(LogFlags.TzSQL, sqlCommand2);
				int num2 = sqlCommand2.ExecuteNonQuery();
				mLogFile.Write(LogFlags.TzINFORMATION, "{0} records updated in backup server", num2);
			}
			result = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			mLogFile.Write(ex);
		}
		return result;
	}

	public bool SyncHistoryTable(string pServer, string bServer, int HistoryType)
	{
		bool result = false;
		string text = "";
		if (HistoryType == 1)
		{
			text = "History_5sec";
		}
		if (HistoryType == 2)
		{
			text = "History_1min";
		}
		if (HistoryType == 3)
		{
			text = "History_1hour";
		}
		DateTime dateTime = new DateTime(2000, 1, 1);
		DateTime dateTime2 = new DateTime(2000, 1, 1);
		try
		{
			mLogFile.Write(LogFlags.TzINFORMATION, "Synchronizing {0} table", text);
			using (SqlConnection sqlConnection = new SqlConnection(mCnPHistory))
			{
				sqlConnection.Open();
				using SqlCommand sqlCommand = sqlConnection.CreateCommand();
				sqlCommand.CommandText = $"SELECT MAX(USTTimestamp) FROM {pServer}.PointsHistory.dbo.{text}";
				using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
				{
					if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
					{
						dateTime2 = sqlDataReader.GetDateTime(0);
					}
				}
				sqlCommand.CommandText = $"SELECT MAX(USTTimestamp) FROM {bServer}.PointsHistory.dbo.{text}";
				using (SqlDataReader sqlDataReader2 = sqlCommand.ExecuteReader())
				{
					if (sqlDataReader2.Read() && !sqlDataReader2.IsDBNull(0))
					{
						dateTime = sqlDataReader2.GetDateTime(0).AddHours(-12.0);
					}
				}
				sqlCommand.Parameters.Add("@MinTimestamp", SqlDbType.DateTime);
				sqlCommand.Parameters.Add("@MaxTimestamp", SqlDbType.DateTime);
				_ = DateTime.Now;
				while (dateTime < dateTime2)
				{
					sqlCommand.CommandText = string.Format("insert into {1}.PointsHistory.dbo.{2} select P.* from {0}.PointsHistory.dbo.{2} P left join {1}.PointsHistory.dbo.{2} B on P.PointId = B.PointId and P.USTTimestamp = B.USTTimestamp where P.USTTimestamp > @MinTimestamp and P.USTTimestamp <= @MaxTimestamp and B.PointId is null", pServer, bServer, text);
					DateTime dateTime3 = dateTime.AddHours(1.0);
					sqlCommand.Parameters["@MinTimestamp"].Value = dateTime;
					sqlCommand.Parameters["@MaxTimestamp"].Value = dateTime3;
					mLogFile.Write(LogFlags.TzSQL, sqlCommand);
					int num = sqlCommand.ExecuteNonQuery();
					mLogFile.Write(LogFlags.TzINFORMATION, "{0} records inserted in backup server", num);
					dateTime = dateTime3;
				}
			}
			result = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			mLogFile.Write(ex);
		}
		return result;
	}
}
