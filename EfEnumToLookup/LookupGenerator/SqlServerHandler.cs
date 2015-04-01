﻿namespace EfEnumToLookup.LookupGenerator
{
	using System;
	using System.Collections.Generic;
	using System.Data.SqlClient;
	using System.Text;

	class SqlServerHandler : IDbHandler
	{
		/// <summary>
		/// The size of the Name field that will be added to the generated lookup tables.
		/// Adjust to suit your data if required, defaults to 255.
		/// </summary>
		public int NameFieldLength { get; set; }

		/// <summary>
		/// Prefix to add to all the generated tables to separate help group them together
		/// and make them stand out as different from other tables.
		/// Defaults to "Enum_" set to null or "" to not have any prefix.
		/// </summary>
		public string TableNamePrefix { get; set; }

		/// <summary>
		/// Suffix to add to all the generated tables to separate help group them together
		/// and make them stand out as different from other tables.
		/// Defaults to "" set to null or "" to not have any suffix.
		/// </summary>
		public string TableNameSuffix { get; set; }


		public void Apply(LookupDbModel model, Action<string, IEnumerable<SqlParameter>> runSql)
		{
			List<SqlParameter> parameters;
			var sql = BuildSql(model, true, out parameters);
			runSql(sql, parameters);
		}

		public string GenerateMigrationSql(LookupDbModel model)
		{
			List<SqlParameter> parameters;
			return BuildSql(model, false, out parameters);
		}

		private string BuildSql(LookupDbModel model, bool useParameters, out List<SqlParameter> parameters)
		{
			var sql = new StringBuilder();
			sql.AppendLine(CreateTables(model.Lookups));
			sql.AppendLine(PopulateLookups(model.Lookups, useParameters, out parameters));
			sql.AppendLine(AddForeignKeys(model.References));
			return sql.ToString();
		}

		private string CreateTables(IEnumerable<LookupData> enums)
		{
			var sql = new StringBuilder();
			foreach (var lookup in enums)
			{
				sql.AppendFormat(
					@"IF OBJECT_ID('{0}', 'U') IS NULL
begin
	CREATE TABLE [{0}] (Id {2} PRIMARY KEY, Name nvarchar({1}));
	exec sys.sp_addextendedproperty @name=N'MS_Description', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE',
		@level1name=N'{0}', @value=N'Automatically generated. Contents will be overwritten on app startup. Table & contents generated by https://github.com/timabell/ef-enum-to-lookup';
end
",
					TableName(lookup.Name), NameFieldLength, NumericSqlType(lookup.NumericType));
			}
			return sql.ToString();
		}

		private string AddForeignKeys(IEnumerable<EnumReference> refs)
		{
			var sql = new StringBuilder();
			foreach (var enumReference in refs)
			{
				var fkName = string.Format("FK_{0}_{1}", enumReference.ReferencingTable, enumReference.ReferencingField);

				sql.AppendFormat(
					" IF OBJECT_ID('{0}', 'F') IS NULL ALTER TABLE [{1}] ADD CONSTRAINT {0} FOREIGN KEY ([{2}]) REFERENCES [{3}] (Id);\r\n",
					fkName, enumReference.ReferencingTable, enumReference.ReferencingField, TableName(enumReference.EnumType.Name)
				);
			}
			return sql.ToString();
		}

		private string PopulateLookups(IEnumerable<LookupData> lookupData, bool useParameters, out List<SqlParameter> parameters)
		{
			var sql = new StringBuilder();
			sql.AppendLine(string.Format("CREATE TABLE #lookups (Id int, Name nvarchar({0}) COLLATE database_default);", NameFieldLength));
			parameters = new List<SqlParameter>();
			var paramIndex = 0; // parameters have to be numbered across the whole batch
			foreach (var lookup in lookupData)
			{
				IList<SqlParameter> batchParameters;
				sql.AppendLine(PopulateLookup(lookup, useParameters, out batchParameters, ref paramIndex));
				parameters.AddRange(batchParameters);
			}
			sql.AppendLine("DROP TABLE #lookups;");
			return sql.ToString();
		}

		private string PopulateLookup(LookupData lookup, bool useParameters, out IList<SqlParameter> parameters, ref int paramIndex)
		{
			var sql = new StringBuilder();
			parameters = new List<SqlParameter>();
			foreach (var value in lookup.Values)
			{
				var id = value.Id;
				var name = value.Name;
				var idParamName = string.Format("id{0}", paramIndex++);
				var nameParamName = string.Format("name{0}", paramIndex++);
				if (useParameters)
				{
					sql.AppendFormat("INSERT INTO #lookups (Id, Name) VALUES (@{0}, @{1});\r\n", idParamName, nameParamName);
				}
				else
				{
					sql.AppendFormat("INSERT INTO #lookups (Id, Name) VALUES ({0}, N'{1}');\r\n", id, SanitizeSqlString(name));
				}
				parameters.Add(new SqlParameter(idParamName, id));
				parameters.Add(new SqlParameter(nameParamName, name));
			}

			sql.AppendLine(string.Format(@"
MERGE INTO [{0}] dst
	USING #lookups src ON src.Id = dst.Id
	WHEN MATCHED AND src.Name <> dst.Name THEN
		UPDATE SET Name = src.Name
	WHEN NOT MATCHED THEN
		INSERT (Id, Name)
		VALUES (src.Id, src.Name)
	WHEN NOT MATCHED BY SOURCE THEN
		DELETE
;"
				, TableName(lookup.Name)));

			sql.AppendLine("TRUNCATE TABLE #lookups;");
			return sql.ToString();
		}

		private string SanitizeSqlString(string value)
		{
			return value.Replace("'", "''");
		}

		private string TableName(string enumName)
		{
			return string.Format("{0}{1}{2}", TableNamePrefix, enumName, TableNameSuffix);
		}

		private static string NumericSqlType(Type numericType)
		{
			if (numericType == typeof(byte))
			{
				return "tinyint";
			}
			return "int";
		}
	}
}
