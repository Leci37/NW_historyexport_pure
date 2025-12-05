using System;
using System.Data.SqlClient;

namespace HistoryExport;

internal static class SqlDataReaderExtension
{
	public static bool? GetNullableBoolean(this SqlDataReader dr, int n)
	{
		if (!dr.IsDBNull(n))
		{
			return dr.GetBoolean(n);
		}
		return null;
	}

	public static DateTime? GetNullableDateTime(this SqlDataReader dr, int n)
	{
		if (!dr.IsDBNull(n))
		{
			return dr.GetDateTime(n);
		}
		return null;
	}

	public static double? GetNullableDouble(this SqlDataReader dr, int n)
	{
		if (!dr.IsDBNull(n))
		{
			return dr.GetDouble(n);
		}
		return null;
	}

	public static int? GetNullableInt32(this SqlDataReader dr, int n)
	{
		if (!dr.IsDBNull(n))
		{
			return dr.GetInt32(n);
		}
		return null;
	}

	public static long? GetNullableInt64(this SqlDataReader dr, int n)
	{
		if (!dr.IsDBNull(n))
		{
			return dr.GetInt64(n);
		}
		return null;
	}

	public static string GetNullableString(this SqlDataReader dr, int n)
	{
		if (!dr.IsDBNull(n))
		{
			return dr.GetString(n);
		}
		return null;
	}
}
