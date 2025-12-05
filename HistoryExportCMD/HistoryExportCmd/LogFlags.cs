namespace HistoryExportCmd;

public enum LogFlags : uint
{
	TzEXCEPTION = 1u,
	TzINFORMATION = 2u,
	TzSQL = 4u,
	TzERROR = 0x10u,
	TzDEBUG = 0x40u
}
