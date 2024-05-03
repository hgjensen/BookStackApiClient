#r "nuget: Lestaly, 0.58.0"
#r "nuget: MySqlConnector, 2.3.7"
#r "nuget: Dapper, 2.1.35"
#r "nuget: Kokuban, 0.2.0"
using Dapper;
using Lestaly;
using Kokuban;
using MySqlConnector;

await Paved.RunAsync(config: c => c.AnyPause(), action: async () =>
{
    WriteLine("Open database");
    var connector = new MySqlConnectionStringBuilder();
    connector.Server = "localhost";
    connector.Port = 9987;
    connector.UserID = "bookstack_user";
    connector.Password = "bookstack_pass";
    connector.Database = "bookstack_store";

    using var db = new MySqlConnection(connector.ConnectionString);
    await db.OpenAsync();

    WriteLine("Query the definition of permissions.");
    var permissions = await db.QueryAsync(
        sql: "select id, name, display_name from role_permissions order by id",
        map: (uint id, string name, string disp) => new { id, name, disp, },
        splitOn: "*"
    );

    static string toCamelCase(string word) => string.Create(word.Length, word, (buf, word) =>
    {
        if (word.Length <= 0) return;
        buf[0] = char.ToUpperInvariant(word[0]);
        word[1..].CopyTo(buf[1..]);
    });

    WriteLine("Generate source code");
    var defines = permissions
        .Select(perm =>
        {
            var words = perm.disp.Replace("&", "And").Split(" ");
            var identity = words.Select(w => toCamelCase(w)).JoinString();
            return $$"""
                public static string {{identity}} { get; } = "{{perm.name}}";
            """;
        })
        .JoinString(Environment.NewLine);

    var source = $$"""
    namespace BookStackApiClient;

    #pragma warning disable CS1591 // 公開されている型またはメンバーの XML コメントがありません

    /// <summary>
    /// ロール権限定数
    /// </summary>
    public static class RolePermissions
    {
    {{defines}}
    }
    """;

    WriteLine("Save the generated source code");
    var genEnc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    var genFile = ThisSource.RelativeFile("../src/BookStackConstants.cs");
    await genFile.WriteAllTextAsync(source.ReplaceLineEndings(), genEnc);

    WriteLine(Chalk.Green["Completed."]);
});
