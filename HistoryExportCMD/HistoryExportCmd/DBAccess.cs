using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data.SqlClient;
using System.Text;

namespace HistoryExportCmd;

internal class DBAccess
{
	private string mCnPHistory;

	private string mCnEbiOdbc;

	private string mCnEbiSql;

	private int mFnType;

	private LogFile mLogFile;

	private SqlConnection mConn;

	public DBAccess(LogFile logFile, string cnEbiSql, string cnEbiOdbc, string cnPointsHistory)
	{
		mCnEbiSql = cnEbiSql;
		mCnEbiOdbc = cnEbiOdbc;
		mCnPHistory = cnPointsHistory;
		mLogFile = logFile;
	}

	public bool Calculate(DateTime iniTimestamp, DateTime endTimestamp)
	{
		bool result = false;
		Console.WriteLine("Calculate: from {0} to {1}", iniTimestamp, endTimestamp.AddMinutes(-1.0));
		try
		{
			using SqlConnection sqlConnection = new SqlConnection(mCnPHistory);
			sqlConnection.Open();
			using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
			{
				string commandText = "INSERT INTO History_15min (USTTimestamp, Timestamp, PointName, ParamName, Value) SELECT DATEADD(MINUTE,(DATEPART(MINUTE,USTTimestamp)/15)*15,DATEADD(HOUR,DATEPART(HOUR,USTTimestamp),DATEADD(DAY,DATEPART(DAY,USTTimestamp)-1,DATEADD(MONTH,DATEPART(MONTH,USTTimestamp)-1,DATEADD(YEAR,DATEPART(YEAR,USTTimestamp)-1900,0))))) USTTimestamp, DATEADD(MINUTE,(DATEPART(MINUTE,Timestamp)/15)*15,DATEADD(HOUR,DATEPART(HOUR,Timestamp),DATEADD(DAY,DATEPART(DAY,Timestamp)-1,DATEADD(MONTH,DATEPART(MONTH,Timestamp)-1,DATEADD(YEAR,DATEPART(YEAR,Timestamp)-1900,0))))) Timestamp, PointName, ParamName, AVG(Value) Avg FROM History_1min WHERE USTTimestamp >= @FROM AND USTTimestamp < @TO GROUP BY DATEADD(MINUTE,(DATEPART(MINUTE,USTTimestamp)/15)*15,DATEADD(HOUR,DATEPART(HOUR,USTTimestamp),DATEADD(DAY,DATEPART(DAY,USTTimestamp)-1,DATEADD(MONTH,DATEPART(MONTH,USTTimestamp)-1,DATEADD(YEAR,DATEPART(YEAR,USTTimestamp)-1900,0))))), DATEADD(MINUTE,(DATEPART(MINUTE,Timestamp)/15)*15,DATEADD(HOUR,DATEPART(HOUR,Timestamp),DATEADD(DAY,DATEPART(DAY,Timestamp)-1,DATEADD(MONTH,DATEPART(MONTH,Timestamp)-1,DATEADD(YEAR,DATEPART(YEAR,Timestamp)-1900,0))))), PointName, ParamName ";
				sqlCommand.CommandText = commandText;
				sqlCommand.Parameters.AddWithValue("FROM", iniTimestamp);
				sqlCommand.Parameters.AddWithValue("TO", endTimestamp);
				sqlCommand.ExecuteNonQuery();
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

	public bool GetEBIStatus(out bool primary)
	{
		primary = false;
		try
		{
			using SqlConnection sqlConnection = new SqlConnection(mCnEbiSql);
			sqlConnection.Open();
			using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
			{
				if (mFnType == 0)
				{
					sqlCommand.CommandText = "SELECT OBJECT_ID('hwsystem.dbo.hsc_sp_IsPrimary')";
					using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
					if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
					{
						mFnType = 1;
					}
				}
				if (mFnType == 0)
				{
					sqlCommand.CommandText = "SELECT OBJECT_ID('hwsystem.dbo.hsc_mfn_IsPrimary')";
					using SqlDataReader sqlDataReader2 = sqlCommand.ExecuteReader();
					if (sqlDataReader2.Read() && !sqlDataReader2.IsDBNull(0))
					{
						mFnType = 2;
					}
				}
				if (mFnType == 0)
				{
					mLogFile.Write(LogFlags.TzERROR, "Couldn't find the method to determine the primary EBI");
				}
				if (mFnType == 1)
				{
					string commandText = "EXEC hwsystem.dbo.hsc_sp_IsPrimary";
					sqlCommand.CommandText = commandText;
					mLogFile.Write(LogFlags.TzSQL, sqlCommand);
					using SqlDataReader sqlDataReader3 = sqlCommand.ExecuteReader();
					if (sqlDataReader3.Read())
					{
						primary = sqlDataReader3.GetInt16(0) != 0;
					}
				}
				if (mFnType == 2)
				{
					string commandText2 = "SELECT hwsystem.dbo.hsc_mfn_IsPrimary()";
					sqlCommand.CommandText = commandText2;
					mLogFile.Write(LogFlags.TzSQL, sqlCommand);
					using SqlDataReader sqlDataReader4 = sqlCommand.ExecuteReader();
					if (sqlDataReader4.Read())
					{
						primary = sqlDataReader4.GetBoolean(0);
					}
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			mLogFile.Write(LogFlags.TzEXCEPTION, ex.ToString());
			return false;
		}
	}

	public bool GetLastDatetime(int HistoryType, out DateTime lastDatetime)
	{
		bool result = false;
		lastDatetime = new DateTime(2000, 1, 1);
		try
		{
			using SqlConnection sqlConnection = new SqlConnection(mCnPHistory);
			sqlConnection.Open();
			using SqlCommand sqlCommand = sqlConnection.CreateCommand();
			if (HistoryType == 1)
			{
				sqlCommand.CommandText = "SELECT MAX(USTTimestamp) FROM History_5sec";
			}
			if (HistoryType == 2)
			{
				sqlCommand.CommandText = "SELECT MAX(USTTimestamp) FROM History_1min";
			}
			if (HistoryType == 3)
			{
				sqlCommand.CommandText = "SELECT MAX(USTTimestamp) FROM History_1hour";
			}
			using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
			if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
			{
				lastDatetime = sqlDataReader.GetDateTime(0);
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

	public bool GetParameter(string name, out bool value)
	{
		bool result = false;
		value = false;
		try
		{
			using SqlConnection sqlConnection = new SqlConnection(mCnPHistory);
			sqlConnection.Open();
			using SqlCommand sqlCommand = sqlConnection.CreateCommand();
			sqlCommand.CommandText = "SELECT Value FROM Parameter WHERE Name = @Name";
			sqlCommand.Parameters.AddWithValue("@Name", name);
			using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
			if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
			{
				value = Convert.ToInt32(sqlDataReader.GetString(0)) != 0;
				result = true;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			mLogFile.Write(ex);
		}
		return result;
	}

	public bool GetPoints(out List<Point> points)
	{
		bool result = false;
		points = null;
		try
		{
			using SqlConnection sqlConnection = new SqlConnection(mCnPHistory);
			sqlConnection.Open();
			using SqlCommand sqlCommand = sqlConnection.CreateCommand();
			string text = (sqlCommand.CommandText = "SELECT PointId, PointName, ParamName, HistoryFast, HistorySlow, HistoryExtd, HistoryFastArch, HistorySlowArch, HistoryExtdArch FROM Point WHERE ((HistoryFast = 1) AND (HistoryFastArch = 1)) OR ((HistorySlow = 1) AND (HistorySlowArch = 1)) OR ((HistoryExtd = 1) AND (HistoryExtdArch = 1))");
			mLogFile.Write(LogFlags.TzSQL, text);
			using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
			points = new List<Point>();
			while (sqlDataReader.Read())
			{
				Point point = new Point();
				point.PointId = sqlDataReader.GetInt32(0);
				point.PointName = sqlDataReader.GetString(1);
				point.ParamName = sqlDataReader.GetString(2);
				point.HistoryFast = sqlDataReader.GetNullableBoolean(3) == true;
				point.HistorySlow = sqlDataReader.GetNullableBoolean(4) == true;
				point.HistoryExtd = sqlDataReader.GetNullableBoolean(5) == true;
				point.HistoryFastArch = sqlDataReader.GetNullableBoolean(6) == true;
				point.HistorySlowArch = sqlDataReader.GetNullableBoolean(7) == true;
				point.HistoryExtdArch = sqlDataReader.GetNullableBoolean(8) == true;
				points.Add(point);
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

	public bool GetHistory(int HistoryType, DateTime iniTimestamp, DateTime endTimestamp, List<Point> points, out List<History> lhistory)
	{
		bool result = false;
		lhistory = null;
		string text = null;
		if (HistoryType == 1)
		{
			text = "History5SecondSnapshot";
		}
		if (HistoryType == 2)
		{
			text = "History1MinSnapshot";
		}
		if (HistoryType == 3)
		{
			text = "History1HourSnapshot";
		}
		StringBuilder stringBuilder = new StringBuilder(1000);
		stringBuilder.Append($"Get from {text}: since {iniTimestamp} to {endTimestamp}, ");
		stringBuilder.Append("Points: ");
		for (int i = 0; i < points.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(points[i].PointName + "." + points[i].ParamName);
		}
		Console.WriteLine(stringBuilder.ToString());
		mLogFile.Write(LogFlags.TzINFORMATION, stringBuilder.ToString());
		try
		{
			using OdbcConnection odbcConnection = new OdbcConnection(mCnEbiOdbc);
			odbcConnection.Open();
			using OdbcCommand odbcCommand = odbcConnection.CreateCommand();
			StringBuilder stringBuilder2 = new StringBuilder(500);
			stringBuilder2.Append("SELECT USTTimeStamp, TimeStamp, ");
			for (int j = 0; j < points.Count; j++)
			{
				stringBuilder2.Append(string.Format("Parameter{0:00#}, Value{0:00#}, Quality{0:00#}", j + 1));
				if (j < points.Count - 1)
				{
					stringBuilder2.Append(", ");
				}
				else
				{
					stringBuilder2.Append(" ");
				}
			}
			stringBuilder2.Append("FROM " + text + " ");
			stringBuilder2.Append("WHERE USTTimeStamp >= ? AND USTTimeStamp < ? ");
			for (int k = 0; k < points.Count; k++)
			{
				stringBuilder2.Append($"AND Parameter{k + 1:00#} = '{points[k].PointName}.{points[k].ParamName}' ");
			}
			mLogFile.Write(LogFlags.TzSQL, stringBuilder2.ToString());
			odbcCommand.CommandText = stringBuilder2.ToString();
			odbcCommand.Parameters.AddWithValue("FROM", iniTimestamp);
			odbcCommand.Parameters.AddWithValue("TO", endTimestamp);
			using OdbcDataReader odbcDataReader = odbcCommand.ExecuteReader();
			lhistory = new List<History>();
			while (odbcDataReader.Read())
			{
				int num = 0;
				DateTime dateTime = odbcDataReader.GetDateTime(num++);
				DateTime dateTime2 = odbcDataReader.GetDateTime(num++);
				for (int l = 0; l < points.Count; l++)
				{
					string[] PointParam = odbcDataReader.GetString(num++).Split('.');
					double value = odbcDataReader.GetDouble(num++);
					string text2 = odbcDataReader.GetString(num++);
					if (text2 == "GOOD")
					{
						Point point = points.Find((Point p) => p.PointName == PointParam[0] && p.ParamName == PointParam[1]);
						if (point != null)
						{
							History history = new History();
							history.PointId = point.PointId;
							history.USTTimestamp = dateTime;
							history.Timestamp = dateTime2;
							history.Value = value;
							lhistory.Add(history);
						}
					}
					else
					{
						mLogFile.Write(LogFlags.TzDEBUG, "Point Quality is not GOOD: {0}.{1} {2} {3}", PointParam[0], PointParam[1], text2, dateTime);
					}
				}
			}
			Console.WriteLine("{0} records retrieved", lhistory.Count);
			mLogFile.Write(LogFlags.TzINFORMATION, "{0} records retrieved", lhistory.Count);
			result = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			mLogFile.Write(ex);
		}
		return result;
	}

	public bool Prepare()
	{
		bool result = false;
		try
		{
			if (mConn != null)
			{
				mConn.Dispose();
			}
			mConn = new SqlConnection(mCnPHistory);
			mConn.Open();
			using SqlCommand sqlCommand = mConn.CreateCommand();
			string text = (sqlCommand.CommandText = "CREATE TABLE #History (PointId int NOT NULL, USTTimestamp datetime NOT NULL, Timestamp datetime NULL, Value float NULL)");
			mLogFile.Write(LogFlags.TzSQL, text);
			sqlCommand.ExecuteNonQuery();
			result = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			mLogFile.Write(ex);
		}
		return result;
	}

	public bool StoreHistory(int HistoryType, List<History> lhistory)
	{
		bool result = false;
		Console.WriteLine("Store {0} records", lhistory.Count);
		mLogFile.Write(LogFlags.TzINFORMATION, "Store {0} records", lhistory.Count);
		try
		{
			using (SqlCommand sqlCommand = mConn.CreateCommand())
			{
				string text = (sqlCommand.CommandText = "INSERT INTO #History (PointId, USTTimestamp, Timestamp, Value) VALUES (@PointId, @USTTimestamp, @Timestamp, @Value)");
				mLogFile.Write(LogFlags.TzSQL, text);
				foreach (History item in lhistory)
				{
					try
					{
						sqlCommand.Parameters.Clear();
						sqlCommand.Parameters.AddWithValue("@PointId", item.PointId);
						sqlCommand.Parameters.AddWithValue("@USTTimestamp", item.USTTimestamp);
						sqlCommand.Parameters.AddWithValue("@Timestamp", item.Timestamp);
						sqlCommand.Parameters.AddWithValue("@Value", item.Value);
						sqlCommand.ExecuteNonQuery();
					}
					catch (Exception ex)
					{
						mLogFile.Write(ex);
					}
				}
			}
			result = true;
		}
		catch (Exception ex2)
		{
			Console.WriteLine(ex2.Message);
			mLogFile.Write(ex2);
		}
		return result;
	}

	public bool Finish(int HistoryType)
	{
		bool result = false;
		string text = null;
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
		Console.WriteLine("Move records to {0}", text);
		mLogFile.Write(LogFlags.TzINFORMATION, "Move records to {0}", text);
		try
		{
			using (SqlCommand sqlCommand = mConn.CreateCommand())
			{
				string text2 = (sqlCommand.CommandText = "INSERT INTO " + text + " (PointId, USTTimestamp, Timestamp, Value) SELECT PointId, USTTimestamp, Timestamp, Value FROM #History");
				sqlCommand.CommandTimeout = 600;
				mLogFile.Write(LogFlags.TzSQL, text2);
				sqlCommand.ExecuteNonQuery();
			}
			mConn.Dispose();
			mConn = null;
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
