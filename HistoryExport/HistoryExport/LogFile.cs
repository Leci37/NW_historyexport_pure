using System;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace HistoryExport;

public class LogFile
{
	private static string m_sLogPath = ".";

	private static uint m_sMaxDays = 15u;

	private static uint m_sMask = uint.MaxValue;

	private uint m_MaxDays;

	private uint m_Mask;

	private object m_Lock;

	private string m_Filename;

	private string m_CurrentFilename;

	private int m_CurrentDay;

	public static void SetConfig(Type flags, NameValueCollection config)
	{
		m_sMask = GetMask(flags, config);
		if (config["LogPath"] != null)
		{
			m_sLogPath = Convert.ToString(config["LogPath"]);
		}
		if (config["LogMaxDays"] != null && uint.TryParse(config["LogMaxDays"], out var val))
		{
			m_sMaxDays = val;
		}
	}

	public static void SetLogPath(string logPath)
	{
		m_sLogPath = logPath;
	}

	public LogFile(string Filename)
	{
		m_Lock = new object();
		if (Path.IsPathRooted(Filename))
		{
			m_Filename = Filename;
		}
		else
		{
			m_Filename = m_sLogPath + "\\" + Filename;
		}
		m_CurrentDay = 0;
		m_Mask = m_sMask;
		m_MaxDays = m_sMaxDays;
	}

	public LogFile(string Filename, uint logMaxDays, Type flags, NameValueCollection config)
	{
		m_Lock = new object();
		m_Filename = Filename;
		m_MaxDays = logMaxDays;
		m_CurrentDay = 0;
		m_Mask = GetMask(flags, config);
	}

	public void Write(Exception ex)
	{
		Write(LogFlags.TzEXCEPTION, ex.ToString());
	}

	public void Write(Enum flag, string text, params object[] pars)
	{
		string aux = string.Format(text, pars);
		Write(flag, aux);
	}

	public void Write(Enum flag, string text)
	{
		if ((Convert.ToUInt32(flag) & m_Mask) != 0)
		{
			Write(text);
		}
	}

	public void Write(Enum flag, SqlCommand cmd)
	{
		if ((Convert.ToUInt32(flag) & m_Mask) == 0)
		{
			return;
		}
		string text = "\n";
		foreach (SqlParameter param in cmd.Parameters)
		{
			text = ((param.SqlValue != null) ? ((!param.SqlDbType.Equals(SqlDbType.DateTime)) ? ((!param.SqlDbType.Equals(SqlDbType.NVarChar)) ? ((!param.SqlDbType.Equals(SqlDbType.Bit)) ? (text + string.Format("declare {0} {1}; set {0} = {2};\n", param.ParameterName, param.SqlDbType, param.SqlValue)) : (text + string.Format("declare {0} {1}; set {0} = {2};\n", param.ParameterName, param.SqlDbType, ((bool)param.Value) ? 1 : 0))) : (text + string.Format("declare {0} {1}({3}); set {0} = '{2}';\n", param.ParameterName, param.SqlDbType, param.SqlValue, param.Value.ToString().Length + 1))) : (text + string.Format("declare {0} {1}; set {0} = '{2:yyyy/MM/dd HH:mm:ss}';\n", param.ParameterName, param.SqlDbType, param.Value))) : (text + string.Format("declare {0} {1}; set {0} = NULL;\n", param.ParameterName, param.SqlDbType)));
		}
		text += cmd.CommandText;
		Write(text);
	}

	public void Write(string text)
	{
		lock (m_Lock)
		{
			try
			{
				DateTime now = DateTime.Now;
				if (now.Day != m_CurrentDay)
				{
					RemoveOldFiles();
					string date = now.ToString("yyyyMMdd");
					m_CurrentFilename = m_Filename.Replace("#", date);
					m_CurrentDay = now.Day;
				}
				string textToWrite = now.ToString("G") + " " + text;
				StreamWriter streamWriter = File.AppendText(m_CurrentFilename);
				streamWriter.WriteLine(textToWrite);
				streamWriter.Close();
			}
			catch
			{
			}
		}
	}

	private void RemoveOldFiles()
	{
		string date = DateTime.Now.AddDays(0L - (long)m_MaxDays).ToString("yyyyMMdd");
		string filename = Path.GetFileName(m_Filename.Replace("#", date));
		FileInfo[] files = new DirectoryInfo(Path.GetDirectoryName(m_Filename)).GetFiles(Path.GetFileName(m_Filename.Replace("#", "*")));
		foreach (FileInfo f in files)
		{
			if (f.Name.CompareTo(filename) < 0)
			{
				f.Delete();
			}
		}
	}

	private static uint GetMask(Type flags, NameValueCollection config)
	{
		uint mask = 0u;
		string[] names = Enum.GetNames(flags);
		foreach (string flagName in names)
		{
			uint conf = 1u;
			int x = Convert.ToInt32(Math.Round(Math.Log(Convert.ToUInt32(Enum.Parse(flags, flagName))) / Math.Log(2.0)));
			if (config[flagName] != null && uint.TryParse(config[flagName], out conf) && conf != 0)
			{
				conf = 1u;
			}
			mask |= conf << x;
		}
		return mask;
	}
}
