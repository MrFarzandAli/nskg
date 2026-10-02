CREATE   PROCEDURE dbo.sp_ProcessTrialBalance
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
    BEGIN TRY
        IF (@Cocode IS NOT NULL AND @Cocode <> '' AND (@CompanyId IS NULL OR @CompanyId = 0))
            SELECT TOP 1 @CompanyId = Id FROM Companies WHERE Cocode = @Cocode OR CAST(Id AS VARCHAR) = @Cocode;
        ELSE IF (@CompanyId IS NOT NULL AND @CompanyId > 0 AND (@Cocode IS NULL OR @Cocode = ''))
            SELECT TOP 1 @Cocode = Cocode FROM Companies WHERE Id = @CompanyId;

        IF (@FinancialYearId IS NOT NULL AND @FinancialYearId > 0)
            SELECT TOP 1 
                @SDate = ISNULL(@SDate, CAST(StartDate AS DATE)),
                @TDate = ISNULL(@TDate, CAST(EndDate AS DATE))
            FROM FinancialYears WHERE Id = @FinancialYearId;

        IF @TDate IS NULL SET @TDate = CAST(GETDATE() AS DATE);
        IF @SDate IS NULL
        BEGIN
            IF MONTH(@TDate) >= 7 SET @SDate = DATEFROMPARTS(YEAR(@TDate), 7, 1);
            ELSE SET @SDate = DATEFROMPARTS(YEAR(@TDate)-1, 7, 1);
        END

        ;WITH SumVoDet AS (
            SELECT vono, cocode, votype, SUM(ISNULL(dramt,0)-ISNULL(cramt,0)) AS NetDiff
            FROM VoDet WHERE ISNULL(IsDeleted,0)=0
              AND (@Cocode IS NULL OR @Cocode='' OR Cocode=@Cocode)
            GROUP BY vono, cocode, votype
        )
        UPDATE h SET
            hdramt = CASE WHEN s.NetDiff<0 THEN ABS(s.NetDiff) ELSE 0 END,
            hcramt = CASE WHEN s.NetDiff>0 THEN ABS(s.NetDiff) ELSE 0 END
        FROM VoHead h INNER JOIN SumVoDet s ON h.vono=s.vono AND h.cocode=s.cocode AND h.votype=s.votype
        WHERE h.votype<>'JV' AND (@Cocode IS NULL OR @Cocode='' OR h.Cocode=@Cocode);

        UPDATE h SET h.AccCode=RTRIM(p.Accode)
        FROM ISSHEAD h INNER JOIN AcPara p ON p.ACTYPE='S' AND (p.Cocode=h.CoCode OR p.CompanyId=h.CompanyId)
        WHERE (@Cocode IS NULL OR @Cocode='' OR h.CoCode=@Cocode OR h.CompanyId=@CompanyId);

        UPDATE c SET c.AcCode=RTRIM(p.Accode)
        FROM CommHead c INNER JOIN AcPara p ON p.ACTYPE='S' AND (p.Cocode=CAST(c.CompanyId AS VARCHAR) OR p.CompanyId=c.CompanyId)
        WHERE (@CompanyId IS NULL OR @CompanyId=0 OR c.CompanyId=@CompanyId);

        UPDATE d SET d.AccCode=RTRIM(p.Accode)
        FROM ISSDETAIL d INNER JOIN AcPara p ON p.ACTYPE='S' AND p.CompanyId=d.CompanyId
        WHERE (@CompanyId IS NULL OR @CompanyId=0 OR d.CompanyId=@CompanyId);

        CREATE TABLE #tmppl(cocode VARCHAR(50),ac1 VARCHAR(10),ac2 VARCHAR(10),ac3 VARCHAR(10),actype VARCHAR(10),opening DECIMAL(18,2));
        CREATE TABLE #tmpbl(cocode VARCHAR(50),ac1 VARCHAR(10),ac2 VARCHAR(10),ac3 VARCHAR(10),actype VARCHAR(10),opening DECIMAL(18,2));

        INSERT INTO #tmppl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT cocode,RTRIM(ac1),'',RTRIM(ac3),RTRIM(actype),SUM(ISNULL(dramt,0))
        FROM VoDet WHERE vodate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR cocode=@Cocode)
          AND RTRIM(actype)='E' AND ISNULL(dramt,0)<>0 AND ISNULL(IsDeleted,0)=0
        GROUP BY cocode,RTRIM(ac1),RTRIM(ac3),RTRIM(actype);

        INSERT INTO #tmppl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT cocode,RTRIM(ac1),'',RTRIM(ac3),RTRIM(actype),-1*SUM(ISNULL(cramt,0))
        FROM VoDet WHERE vodate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR cocode=@Cocode)
          AND RTRIM(ac1)='057' AND votype='CR' AND ISNULL(IsDeleted,0)=0
        GROUP BY cocode,RTRIM(ac1),RTRIM(ac3),RTRIM(actype);

        INSERT INTO #tmpbl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT h.CoCode,SUBSTRING(h.CusCode,1,3),'',SUBSTRING(h.CusCode,4,3),
               ISNULL(NULLIF(RTRIM(g.AcType),''),'A'),-1*SUM(ISNULL(h.NetAmt,0))
        FROM ISSHEAD h
        LEFT JOIN GLChart3 g ON RTRIM(g.AC1)=SUBSTRING(h.CusCode,1,3)
            AND RTRIM(g.AC3)=SUBSTRING(h.CusCode,4,3)
            AND (g.CoCode=h.CoCode OR g.CompanyId=h.CompanyId)
        WHERE h.DocDate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR h.CoCode=@Cocode OR h.CompanyId=@CompanyId)
          AND h.DocDate IS NOT NULL AND h.PType='Paid' AND ISNULL(h.IsDeleted,0)=0
        GROUP BY h.CoCode,SUBSTRING(h.CusCode,1,3),SUBSTRING(h.CusCode,4,3),ISNULL(NULLIF(RTRIM(g.AcType),''),'A');

        INSERT INTO #tmpbl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT ISNULL(c.CoCode,@Cocode),SUBSTRING(ch.StationCode,1,3),'',SUBSTRING(ch.StationCode,4,3),
               ISNULL(NULLIF(RTRIM(ch.AcType),''),'L'),-1*SUM(ISNULL(ch.StationAmt,0))
        FROM CommHead ch LEFT JOIN Companies c ON ch.CompanyId=c.Id
        WHERE ch.DocDate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR c.CoCode=@Cocode OR ch.CompanyId=@CompanyId)
          AND ch.StationAmt IS NOT NULL AND ch.StationAmt>0
          AND LEN(RTRIM(ISNULL(ch.StationCode,'')))>=6 AND ISNULL(ch.IsDeleted,0)=0
        GROUP BY ISNULL(c.CoCode,@Cocode),SUBSTRING(ch.StationCode,1,3),SUBSTRING(ch.StationCode,4,3),ISNULL(NULLIF(RTRIM(ch.AcType),''),'L');

        INSERT INTO #tmpbl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT ISNULL(c.CoCode,@Cocode),SUBSTRING(ch.TransCode,1,3),'',SUBSTRING(ch.TransCode,4,3),
               ISNULL(NULLIF(RTRIM(ch.AcType),''),'L'),-1*SUM(ISNULL(ch.TransporterAmt,0))
        FROM CommHead ch LEFT JOIN Companies c ON ch.CompanyId=c.Id
        WHERE ch.DocDate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR c.CoCode=@Cocode OR ch.CompanyId=@CompanyId)
          AND ch.TransporterAmt IS NOT NULL AND ch.TransporterAmt>0
          AND LEN(RTRIM(ISNULL(ch.TransCode,'')))>=6 AND ISNULL(ch.IsDeleted,0)=0
        GROUP BY ISNULL(c.CoCode,@Cocode),SUBSTRING(ch.TransCode,1,3),SUBSTRING(ch.TransCode,4,3),ISNULL(NULLIF(RTRIM(ch.AcType),''),'L');

        INSERT INTO #tmpbl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT ISNULL(c.CoCode,@Cocode),SUBSTRING(ch.AdvanceCode,1,3),'',SUBSTRING(ch.AdvanceCode,4,3),
               ISNULL(NULLIF(RTRIM(ch.AcType),''),'L'),-1*SUM(ISNULL(ch.AdvanceAmt,0))
        FROM CommHead ch LEFT JOIN Companies c ON ch.CompanyId=c.Id
        WHERE ch.DocDate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR c.CoCode=@Cocode OR ch.CompanyId=@CompanyId)
          AND ch.AdvanceAmt IS NOT NULL AND ch.AdvanceAmt<>0
          AND LEN(RTRIM(ISNULL(ch.AdvanceCode,'')))>=6 AND ISNULL(ch.IsDeleted,0)=0
        GROUP BY ISNULL(c.CoCode,@Cocode),SUBSTRING(ch.AdvanceCode,1,3),SUBSTRING(ch.AdvanceCode,4,3),ISNULL(NULLIF(RTRIM(ch.AcType),''),'L');

        INSERT INTO #tmpbl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT ISNULL(c.CoCode,@Cocode),'074','','001','A',SUM(ABS(ISNULL(ch.StationAmt,0)))
        FROM CommHead ch LEFT JOIN Companies c ON ch.CompanyId=c.Id
        WHERE ch.DocDate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR c.CoCode=@Cocode OR ch.CompanyId=@CompanyId)
          AND ch.StationAmt IS NOT NULL AND ch.StationAmt<0 AND ISNULL(ch.IsDeleted,0)=0
        GROUP BY ISNULL(c.CoCode,@Cocode);

        INSERT INTO #tmpbl(cocode,ac1,ac2,ac3,actype,opening)
        SELECT ISNULL(c.CoCode,@Cocode),'074','','002','A',SUM(ABS(ISNULL(ch.TransporterAmt,0)))
        FROM CommHead ch LEFT JOIN Companies c ON ch.CompanyId=c.Id
        WHERE ch.DocDate BETWEEN @SDate AND @TDate
          AND (@Cocode IS NULL OR @Cocode='' OR c.CoCode=@Cocode OR ch.CompanyId=@CompanyId)
          AND ch.TransporterAmt IS NOT NULL AND ch.TransporterAmt<0 AND ISNULL(ch.IsDeleted,0)=0
        GROUP BY ISNULL(c.CoCode,@Cocode);

        -- Unapp: ISSHEAD sales + VoDet I/E only (CommHead.TotNet NOT used - different meaning)
        DECLARE @msal DECIMAL(18,2)=0, @mpl DECIMAL(18,2)=0, @unapp DECIMAL(18,2)=0, @mpnl DECIMAL(18,2)=0;

        SELECT @msal=-1*ISNULL(SUM(ISNULL(NetAmt,0)),0) FROM ISSHEAD
        WHERE DocDate<@SDate
          AND (@Cocode IS NULL OR @Cocode='' OR CoCode=@Cocode OR CompanyId=@CompanyId)
          AND DocDate IS NOT NULL AND ISNULL(IsDeleted,0)=0;

        SELECT @mpl=ISNULL(SUM(ISNULL(dramt,0)-ISNULL(cramt,0)),0) FROM VoDet
        WHERE vodate<@SDate
          AND (@Cocode IS NULL OR @Cocode='' OR cocode=@Cocode)
          AND RTRIM(actype) IN ('I','E') AND ISNULL(IsDeleted,0)=0;

        SET @unapp = ISNULL(@msal,0) + ISNULL(@mpl,0);

        UPDATE GLChart3 SET Opening=0
        WHERE (@Cocode IS NULL OR @Cocode='' OR CoCode=@Cocode OR CompanyId=@CompanyId)
          AND (AcType IS NULL OR AcType<>'S');

        ;WITH AggBL AS(SELECT cocode,ac1,ac3,SUM(ISNULL(opening,0)) AS SumOpening FROM #tmpbl GROUP BY cocode,ac1,ac3)
        UPDATE a SET a.Opening=ISNULL(b.SumOpening,0)
        FROM GLChart3 a INNER JOIN AggBL b ON RTRIM(a.ac1)=b.ac1 AND RTRIM(a.ac3)=b.ac3
            AND (a.CoCode=b.cocode OR @Cocode='' OR @Cocode IS NULL)
        WHERE RTRIM(a.AcType) IN ('A','C','L')
          AND (@Cocode IS NULL OR @Cocode='' OR a.CoCode=@Cocode OR a.CompanyId=@CompanyId)
          AND (a.AcType IS NULL OR a.AcType<>'S');

        ;WITH AggPL AS(SELECT cocode,ac1,ac3,SUM(ISNULL(opening,0)) AS SumOpening FROM #tmppl GROUP BY cocode,ac1,ac3)
        UPDATE a SET a.Opening=ISNULL(b.SumOpening,0)
        FROM GLChart3 a INNER JOIN AggPL b ON RTRIM(a.ac1)=b.ac1 AND RTRIM(a.ac3)=b.ac3
            AND (a.CoCode=b.cocode OR @Cocode='' OR @Cocode IS NULL)
        WHERE RTRIM(a.AcType) IN ('I','E')
          AND (@Cocode IS NULL OR @Cocode='' OR a.CoCode=@Cocode OR a.CompanyId=@CompanyId)
          AND (a.AcType IS NULL OR a.AcType<>'S');

        DECLARE @ProfitAccode VARCHAR(50)=NULL;
        SELECT TOP 1 @ProfitAccode=RTRIM(Accode) FROM AcPara
        WHERE RTRIM(ACTYPE)='P'
          AND (@Cocode IS NULL OR @Cocode='' OR Cocode=@Cocode OR CompanyId=@CompanyId);

        IF @ProfitAccode IS NOT NULL
        BEGIN
            SELECT @mpnl=SUM(ISNULL(Opening,0)) FROM GLChart3
            WHERE RTRIM(AC1)+RTRIM(AC3)=@ProfitAccode
              AND (@Cocode IS NULL OR @Cocode='' OR CoCode=@Cocode OR CompanyId=@CompanyId);

            UPDATE GLChart3 SET Opening=ISNULL(@mpnl,0)+ISNULL(@unapp,0)
            WHERE RTRIM(AC1)+RTRIM(AC3)=@ProfitAccode
              AND (@Cocode IS NULL OR @Cocode='' OR CoCode=@Cocode OR CompanyId=@CompanyId);
        END

        ;WITH Bal1 AS(
            SELECT RTRIM(g3.AC1) AS AC1,ISNULL(g3.CoCode,'') AS CoCode,SUM(ISNULL(g3.Opening,0)) AS SumOpening
            FROM GLChart3 g3
            WHERE (@Cocode IS NULL OR @Cocode='' OR g3.CoCode=@Cocode OR g3.CompanyId=@CompanyId)
              AND (g3.AcType IS NULL OR g3.AcType<>'S')
            GROUP BY RTRIM(g3.AC1),ISNULL(g3.CoCode,'')
        )
        UPDATE g1 SET g1.Opening=ISNULL(b.SumOpening,0)
        FROM GLChart1 g1 INNER JOIN Bal1 b ON RTRIM(g1.AC1)=b.AC1
            AND (ISNULL(g1.CoCode,'')=b.CoCode OR @Cocode='' OR @Cocode IS NULL)
        WHERE (@Cocode IS NULL OR @Cocode='' OR g1.CoCode=@Cocode OR g1.CompanyId=@CompanyId)
          AND (g1.AcType IS NULL OR g1.AcType<>'S');

        SELECT 'GLChart1 Summary' AS Report,
            SUM(CASE WHEN ISNULL(Opening,0)>0 THEN Opening ELSE 0 END) AS TotalDr,
            SUM(CASE WHEN ISNULL(Opening,0)<0 THEN ABS(Opening) ELSE 0 END) AS TotalCr,
            SUM(ISNULL(Opening,0)) AS NetBalance
        FROM GLChart1
        WHERE (@Cocode IS NULL OR @Cocode='' OR CoCode=@Cocode OR CompanyId=@CompanyId);

        DROP TABLE #tmppl; DROP TABLE #tmpbl;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF OBJECT_ID('tempdb..#tmppl') IS NOT NULL DROP TABLE #tmppl;
        IF OBJECT_ID('tempdb..#tmpbl') IS NOT NULL DROP TABLE #tmpbl;
        THROW;
    END CATCH;
END;