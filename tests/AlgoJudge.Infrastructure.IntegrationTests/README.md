# AlgoJudge.Infrastructure.IntegrationTests

PostgreSQL repository, migration, and atomic submission-claim tests belong
here. Queue tests create an isolated temporary database, apply all EF Core
migrations, and drop that database after each test.

Set `TEST_POSTGRES_CONNECTION` to an administrative PostgreSQL connection that
can create and drop databases. Tests using `PostgreSqlFact` are skipped when
the variable is absent; Backend CI supplies it through its PostgreSQL service.
