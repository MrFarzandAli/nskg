CREATE OR ALTER PROCEDURE dbo.sp_ProcessTrialBalance
(
    @Cocode VARCHAR(10) = NULL,
    @CompanyId INT = NULL,
    @FinancialYearId INT = NULL,
    @TDate DATE = NULL,
    @SDate DATE = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    
    IF (@CompanyId IS NULL OR @CompanyId = 0) AND (@Cocode IS NOT NULL AND @Cocode <> '' AND UPPER(@Cocode) <> 'ALL')
    BEGIN
        SELECT TOP 1 @CompanyId = Id FROM Companies WHERE Cocode = @Cocode OR CAST(Id AS VARCHAR) = @Cocode;
    END

    IF @TDate IS NULL SET @TDate = CAST(GETDATE() AS DATE);

    DECLARE @compParam VARCHAR(10) = NULL;
    IF @CompanyId IS NOT NULL AND @CompanyId > 0
        SET @compParam = CAST(@CompanyId AS VARCHAR(10));
    ELSE
        SET @compParam = 'ALL';

    EXEC dbo.process_opening_balances @companyid = @compParam, @tdate = @TDate, @placcode = NULL;
    RETURN 0;
END;