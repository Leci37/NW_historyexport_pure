using System;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace HistoryExportCmd;

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
		if (config["LogMaxDays"] != null && uint.TryParse(config["LogMaxDays"], out var result))
		{
			m_sMaxDays = result;
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
		string text2 = string.Format(text, pars);
		Write(flag, text2);
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
		foreach (SqlParameter parameter in cmd.Parameters)
		{
			text = ((parameter.SqlValue != null) ? ((!parameter.SqlDbType.Equals(SqlDbType.DateTime)) ? ((!parameter.SqlDbType.Equals(SqlDbType.NVarChar)) ? ((!parameter.SqlDbType.Equals(SqlDbType.Bit)) ? (text + string.Format("declare {0} {1}; set {0} = {2};\n", parameter.ParameterName, parameter.SqlDbType, parameter.SqlValue)) : (text + string.Format("declare {0} {1}; set {0} = {2};\n", parameter.ParameterName, parameter.SqlDbType, ((bool)parameter.Value) ? 1 : 0))) : (text + string.Format("declare {0} {1}({3}); set {0} = '{2}';\n", parameter.ParameterName, parameter.SqlDbType, parameter.SqlValue, parameter.Value.ToString().Length + 1))) : (text + string.Format("declare {0} {1}; set {0} = '{2:yyyy/MM/dd HH:mm:ss}';\n", parameter.ParameterName, parameter.SqlDbType, parameter.Value))) : (text + string.Format("declare {0} {1}; set {0} = NULL;\n", parameter.ParameterName, parameter.SqlDbType)));
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
					string newValue = now.ToString("yyyyMMdd");
					m_CurrentFilename = m_Filename.Replace("#", newValue);
					m_CurrentDay = now.Day;
				}
				string value = now.ToString("G") + " " + text;
				StreamWriter streamWriter = File.AppendText(m_CurrentFilename);
				streamWriter.WriteLine(value);
				streamWriter.Close();
			}
			catch
			{
			}
		}
	}

	private void RemoveOldFiles()
	{
		string newValue = DateTime.Now.AddDays(0L - (long)m_MaxDays).ToString("yyyyMMdd");
		string fileName = Path.GetFileName(m_Filename.Replace("#", newValue));
		FileInfo[] files = new DirectoryInfo(Path.GetDirectoryName(m_Filename)).GetFiles(Path.GetFileName(m_Filename.Replace("#", "*")));
		foreach (FileInfo fileInfo in files)
		{
			if (fileInfo.Name.CompareTo(fileName) < 0)
			{
				fileInfo.Delete();
			}
		}
	}

	private static uint GetMask(Type flags, NameValueCollection config)
	{
		uint num = 0u;
		string[] names = Enum.GetNames(flags);
		foreach (string text in names)
		{
			uint result = 1u;
			int num2 = Convert.ToInt32(Math.Round(Math.Log(Convert.ToUInt32(Enum.Parse(flags, text))) / Math.Log(2.0)));
			if (config[text] != null && uint.TryParse(config[text], out result) && result != 0)
			{
				result = 1u;
			}
			num |= result << num2;
		}
		return num;
	}
}
