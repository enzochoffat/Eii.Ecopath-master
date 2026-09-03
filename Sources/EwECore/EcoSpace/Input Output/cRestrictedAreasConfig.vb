' ===============================================================================
' This file is part of Ecopath with Ecosim (EwE)
'
' EwE is free software: you can redistribute it and/or modify it under the terms
' of the GNU General Public License version 2 as published by the Free Software 
' Foundation.
'
' EwE is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
' without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR 
' PURPOSE. See the GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License along with EwE.
' If not, see <http://www.gnu.org/licenses/gpl-2.0.html>. 
'
' Copyright 1991- 
'    Ecopath International Initiative, Barcelona, Spain
' ===============================================================================
'

#Region " Imports "

Option Strict On
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

#End Region ' Imports

''' ---------------------------------------------------------------------------
''' <summary>
''' A single restricted area: a named geographic zone, defined by a shapefile.
''' </summary>
''' ---------------------------------------------------------------------------
Public Class cRestrictedAreaZone

    ''' <summary>
    ''' Name of the zone. This name is used as the key in the diatome
    ''' "restricted_area_map" configuration entry.
    ''' </summary>
    Public Property Name As String = ""

    ''' <summary>
    ''' Full path to the zone shapefile (.shp).
    ''' </summary>
    Public Property ShapefilePath As String = ""

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Provide a friendly description for lists and grids.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Overrides Function ToString() As String
        If (String.IsNullOrWhiteSpace(Me.ShapefilePath)) Then Return Me.Name
        Return String.Format("{0}  [{1}]", Me.Name, Me.ShapefilePath)
    End Function

End Class

''' ---------------------------------------------------------------------------
''' <summary>
''' Container for the restricted areas configuration of the FIBE coupling.
''' </summary>
''' <remarks>
''' <para>Holds the list of geographic zones (shapefiles) and the per-fleet,
''' per-year, per-month restriction matrices. The matrices are sparse in the
''' CSV exchange format (only non-open intervals are stored) but dense in
''' memory and in the JSON persistence format.</para>
''' <para>The restriction matrix maps a fleet name (as used by diatome, e.g.
''' "archipelago", "coastal", "trawler") and a simulation year to a zone x
''' month matrix. Month columns run January (0) to December (11). Values follow
''' the diatome encoding: 0 = closed ("fermé"), 1 = navigation only
''' ("navigable"), 2 = open ("ouvert"). Default is open.</para>
''' </remarks>
''' ---------------------------------------------------------------------------
Public Class cRestrictedAreasConfig

    ''' <summary>
    ''' Number of months in the restriction matrix.
    ''' </summary>
    Public Const nMonths As Integer = 12

    ''' <summary>
    ''' Fleet types used by diatome. Each fleet has its own restriction matrices.
    ''' </summary>
    Public Shared ReadOnly FIBEFleetTypes() As String = {"archipelago", "coastal", "trawler"}

    ''' <summary>
    ''' The geographic zones.
    ''' </summary>
    Public Property Zones As List(Of cRestrictedAreaZone) = New List(Of cRestrictedAreaZone)

    ''' <summary>
    ''' Per-fleet, per-year restriction matrices.
    ''' Outer key = fleet name (lowercase), inner key = year (e.g. 2019),
    ''' value = zone x month matrix (jagged array, 2 = open by default).
    ''' </summary>
    Public Property Vector As Dictionary(Of String, Dictionary(Of Integer, Integer()())) =
        New Dictionary(Of String, Dictionary(Of Integer, Integer()()))(StringComparer.OrdinalIgnoreCase)

#Region " Map (zones) serialization "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Serialize the zones to a JSON string, in the format expected by the
    ''' diatome "maps.restricted_area_map" entry:
    ''' <c>{ "zone_1": "path1", "zone_2": "path2" }</c>.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Function SerializeMap() As String
        Dim dict As New Dictionary(Of String, String)
        For Each zone As cRestrictedAreaZone In Me.Zones
            If (Not String.IsNullOrWhiteSpace(zone.Name)) Then
                dict(zone.Name) = zone.ShapefilePath
            End If
        Next
        Return JsonConvert.SerializeObject(dict, Formatting.None)
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Restore the zones from a JSON string.
    ''' </summary>
    ''' <param name="strJSON">JSON in the diatome "restricted_area_map" format,
    ''' or Nothing/empty to restore an empty list.</param>
    ''' -----------------------------------------------------------------------
    Public Sub DeserializeMap(strJSON As String)
        Me.Zones = New List(Of cRestrictedAreaZone)
        If (String.IsNullOrWhiteSpace(strJSON)) Then Return
        Try
            Dim dict As Dictionary(Of String, String) = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(strJSON)
            If (dict Is Nothing) Then Return
            For Each kv As KeyValuePair(Of String, String) In dict
                Me.Zones.Add(New cRestrictedAreaZone With {.Name = kv.Key, .ShapefilePath = kv.Value})
            Next
        Catch
            Me.Zones = New List(Of cRestrictedAreaZone)
        End Try
    End Sub

#End Region ' Map (zones) serialization

#Region " Vector (matrix) serialization "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Serialize the per-fleet, per-year restriction matrices to a JSON string.
    ''' New format: <c>{ "archipelago": { "2019": [[2,2,...],...], "2020": [...] }, ... }</c>.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Function SerializeVector() As String
        Return JsonConvert.SerializeObject(Me.Vector, Formatting.None)
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Restore the per-fleet, per-year matrices from a JSON string.
    ''' Handles both the new per-year format and the legacy per-fleet 12-month
    ''' format (fleet -> matrix). Legacy data is migrated by placing the
    ''' matrix under a sentinel year and will be expanded on next sync.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Sub DeserializeVector(strJSON As String)
        Me.Vector = New Dictionary(Of String, Dictionary(Of Integer, Integer()()))(StringComparer.OrdinalIgnoreCase)
        If (String.IsNullOrWhiteSpace(strJSON)) Then Return
        Try
            Dim jo As JObject = JObject.Parse(strJSON)
            For Each fleetProp As JProperty In jo.Properties()
                Dim fleetKey As String = fleetProp.Name
                Dim fleetVal As JToken = fleetProp.Value
                If (fleetVal.Type = JTokenType.Array) Then
                    ' Legacy: fleet -> matrix (no year dimension).
                    ' Store under year 0 as sentinel so callers can migrate.
                    Dim legacyMatrix As Integer()() = fleetVal.ToObject(Of Integer()())()
                    Dim yearDict As New Dictionary(Of Integer, Integer()())
                    yearDict(0) = legacyMatrix
                    Me.Vector(fleetKey) = yearDict
                ElseIf (fleetVal.Type = JTokenType.Object) Then
                    Dim yearDict As New Dictionary(Of Integer, Integer()())
                    For Each yearProp As JProperty In DirectCast(fleetVal, JObject).Properties()
                        Dim nYear As Integer
                        If (Integer.TryParse(yearProp.Name, nYear)) Then
                            Dim mat As Integer()() = yearProp.Value.ToObject(Of Integer()())()
                            yearDict(nYear) = mat
                        End If
                    Next
                    Me.Vector(fleetKey) = yearDict
                End If
            Next
        Catch
            ' Fallback: try legacy direct deserialization for very old files
            Try
                Dim legacy As Dictionary(Of String, Integer()()) =
                    JsonConvert.DeserializeObject(Of Dictionary(Of String, Integer()()))(strJSON)
                If (legacy IsNot Nothing) Then
                    For Each kv As KeyValuePair(Of String, Integer()()) In legacy
                        Dim d As New Dictionary(Of Integer, Integer()())
                        d(0) = kv.Value
                        Me.Vector(kv.Key) = d
                    Next
                End If
            Catch
                Me.Vector = New Dictionary(Of String, Dictionary(Of Integer, Integer()()))(StringComparer.OrdinalIgnoreCase)
            End Try
        End Try
    End Sub

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Returns a zone x month matrix for a fleet and year, creating it when
    ''' missing. New matrices are filled with <see cref="eRestrictedAreaStatus.Open"/>.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Function GetOrCreateVector(fleetName As String, year As Integer, nZones As Integer) As Integer()()
        Dim fleetKey As String = fleetName.Trim().ToLowerInvariant()
        Dim yearDict As Dictionary(Of Integer, Integer()()) = Nothing
        If (Not Me.Vector.TryGetValue(fleetKey, yearDict)) Then
            yearDict = New Dictionary(Of Integer, Integer()())
            Me.Vector(fleetKey) = yearDict
        End If
        Dim matrix As Integer()() = Nothing
        If (yearDict.TryGetValue(year, matrix)) Then
            ' Ensure row count still matches current zone count
            If (matrix.Length = nZones) Then Return matrix
            ' Resize: keep existing rows, add new ones as open
            Dim resized(nZones - 1)() As Integer
            For i As Integer = 0 To nZones - 1
                If (i < matrix.Length) Then
                    resized(i) = matrix(i)
                    ' Ensure 12 columns
                    If (resized(i) Is Nothing OrElse resized(i).Length <> nMonths) Then
                        resized(i) = CreateOpenRow()
                    End If
                Else
                    resized(i) = CreateOpenRow()
                End If
            Next
            yearDict(year) = resized
            Return resized
        End If
        ' Create new matrix for this fleet/year
        Dim newMat(nZones - 1)() As Integer
        For i As Integer = 0 To nZones - 1
            newMat(i) = CreateOpenRow()
        Next
        yearDict(year) = newMat
        Return newMat
    End Function

    ''' <summary>Create a single row (12 months) filled with Open.</summary>
    Private Shared Function CreateOpenRow() As Integer()
        Dim row(nMonths - 1) As Integer
        For m As Integer = 0 To nMonths - 1
            row(m) = CInt(eRestrictedAreaStatus.Open)
        Next
        Return row
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Ensure that every fleet has a matrix for each year in the inclusive
    ''' range [firstYear, lastYear]. Missing years are created as all-open.
    ''' Legacy sentinel year 0, if present, is expanded to all years.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Sub EnsureYearRange(firstYear As Integer, lastYear As Integer, nZones As Integer)
        If (firstYear > lastYear) Then Return
        For Each fleet As String In FIBEFleetTypes
            Dim fleetKey As String = fleet.ToLowerInvariant()
            Dim yearDict As Dictionary(Of Integer, Integer()()) = Nothing
            If (Not Me.Vector.TryGetValue(fleetKey, yearDict)) Then
                yearDict = New Dictionary(Of Integer, Integer()())
                Me.Vector(fleetKey) = yearDict
            End If
            ' Migrate legacy sentinel year 0 -> replicate to all years
            Dim legacyMat As Integer()() = Nothing
            Dim hasLegacy As Boolean = yearDict.TryGetValue(0, legacyMat)
            For yr As Integer = firstYear To lastYear
                If (Not yearDict.ContainsKey(yr)) Then
                    If (hasLegacy AndAlso legacyMat IsNot Nothing) Then
                        ' Clone legacy matrix
                        Dim clone(legacyMat.Length - 1)() As Integer
                        For i As Integer = 0 To legacyMat.Length - 1
                            clone(i) = CType(legacyMat(i).Clone(), Integer())
                        Next
                        ' Resize if zone count changed
                        If (clone.Length <> nZones) Then
                            Dim resized(nZones - 1)() As Integer
                            For i As Integer = 0 To nZones - 1
                                If (i < clone.Length) Then
                                    resized(i) = clone(i)
                                Else
                                    resized(i) = CreateOpenRow()
                                End If
                            Next
                            clone = resized
                        End If
                        yearDict(yr) = clone
                    Else
                        yearDict(yr) = CreateMatrix(nZones)
                    End If
                Else
                    ' Ensure existing year matrix has correct zone count
                    Dim mat As Integer()() = yearDict(yr)
                    If (mat.Length <> nZones) Then
                        Dim resized(nZones - 1)() As Integer
                        For i As Integer = 0 To nZones - 1
                            If (i < mat.Length) Then
                                resized(i) = mat(i)
                            Else
                                resized(i) = CreateOpenRow()
                            End If
                        Next
                        yearDict(yr) = resized
                    End If
                End If
            Next
            If (hasLegacy) Then yearDict.Remove(0)
        Next
    End Sub

    Private Shared Function CreateMatrix(nZones As Integer) As Integer()()
        If (nZones <= 0) Then Return New Integer(-1)() {}
        Dim m(nZones - 1)() As Integer
        For i As Integer = 0 To nZones - 1
            m(i) = CreateOpenRow()
        Next
        Return m
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Get the set of years that have data for at least one fleet.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Function GetAllYears() As List(Of Integer)
        Dim years As New SortedSet(Of Integer)
        For Each fleetDict As Dictionary(Of Integer, Integer()()) In Me.Vector.Values
            For Each yr As Integer In fleetDict.Keys
                If (yr <> 0) Then years.Add(yr)
            Next
        Next
        Return New List(Of Integer)(years)
    End Function

#End Region ' Vector (matrix) serialization

#Region " Sparse CSV import / export "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Try to parse a month/year string in either MM/YYYY or YYYY-MM format.
    ''' Accepts "/" or "-" as separator, trims whitespace.
    ''' </summary>
    ''' <param name="s">Input like "06/2019" or "2019-06".</param>
    ''' <param name="year">Parsed year on success.</param>
    ''' <param name="month">Parsed month 1..12 on success.</param>
    ''' <returns>True if parsed.</returns>
    ''' -----------------------------------------------------------------------
    Public Shared Function TryParseMonthYear(s As String, ByRef year As Integer, ByRef month As Integer) As Boolean
        year = 0 : month = 0
        If (String.IsNullOrWhiteSpace(s)) Then Return False
        Dim t As String = s.Trim()
        ' Split on "/" or "-"
        Dim sep As Char() = {"/"c, "-"c, "\"c}
        Dim parts() As String = t.Split(sep, StringSplitOptions.RemoveEmptyEntries)
        If (parts.Length <> 2) Then Return False
        parts(0) = parts(0).Trim()
        parts(1) = parts(1).Trim()
        Dim p0 As Integer, p1 As Integer
        If (Not Integer.TryParse(parts(0), p0)) Then Return False
        If (Not Integer.TryParse(parts(1), p1)) Then Return False
        ' Decide which is year vs month by magnitude: year is 4-digit (1000-3000)
        If (p0 >= 1000 AndAlso p0 <= 3000) Then
            year = p0 : month = p1
        ElseIf (p1 >= 1000 AndAlso p1 <= 3000) Then
            year = p1 : month = p0
        Else
            Return False
        End If
        If (month < 1 OrElse month > 12) Then Return False
        If (year < 1900 OrElse year > 3000) Then Return False
        Return True
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Map a status display string to <see cref="eRestrictedAreaStatus"/>.
    ''' Accepts FR and EN, case-insensitive, accent-tolerant for "fermé"/"ferme".
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Shared Function StatusFromString(s As String) As eRestrictedAreaStatus
        If (String.IsNullOrWhiteSpace(s)) Then Return eRestrictedAreaStatus.Open
        Dim t As String = s.Trim().ToLowerInvariant()
        ' Normalize accents: replace é->e, è->e
        t = t.Replace("é", "e").Replace("è", "e")
        Select Case t
            Case "ferme", "closed", "ferme".Replace("é", "e")
                Return eRestrictedAreaStatus.Closed
            Case "navigable", "navigation"
                Return eRestrictedAreaStatus.Navigation
            Case "ouvert", "open"
                Return eRestrictedAreaStatus.Open
            Case Else
                ' Try numeric codes 0/1/2 as fallback
                Dim n As Integer
                If (Integer.TryParse(t, n)) Then
                    If (n = 0) Then Return eRestrictedAreaStatus.Closed
                    If (n = 1) Then Return eRestrictedAreaStatus.Navigation
                    If (n = 2) Then Return eRestrictedAreaStatus.Open
                End If
                Return eRestrictedAreaStatus.Open
        End Select
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Check if a status string is valid (closed/navigation/open in FR or EN).
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Shared Function IsValidStatusString(s As String) As Boolean
        If (String.IsNullOrWhiteSpace(s)) Then Return False
        Dim t As String = s.Trim().ToLowerInvariant().Replace("é", "e").Replace("è", "e")
        Return t = "ferme" OrElse t = "closed" OrElse t = "navigable" OrElse t = "navigation" OrElse t = "ouvert" OrElse t = "open" OrElse t = "0" OrElse t = "1" OrElse t = "2"
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Import a sparse interval CSV.
    ''' Expected columns (order flexible, header required, case-insensitive,
    ''' FR or EN, ";" delimiter):
    '''   flottille/fleet | zone | debut/start | fin/end | statut/status
    ''' Dates are MM/YYYY or YYYY-MM, inclusive. Only non-open rows are stored;
    ''' rows with statut "ouvert"/"open" are silently ignored. Conflicting
    ''' intervals (same fleet/zone/month with different non-open statuses) raise
    ''' an error with line numbers.
    ''' </summary>
    ''' <param name="filePath">Path to the CSV file (latin-1).</param>
    ''' <param name="errMsg">Error message on failure, including line numbers.</param>
    ''' <returns>True on success.</returns>
    ''' -----------------------------------------------------------------------
    Public Function ImportSparseCsv(filePath As String, ByRef errMsg As String) As Boolean
        errMsg = ""
        If (Not File.Exists(filePath)) Then
            errMsg = String.Format("File not found: {0}", filePath)
            Return False
        End If
        ' Read with latin-1 (diatome uses it) but also handle UTF-8 BOM
        Dim latin1 As Encoding = Encoding.GetEncoding(28591)
        Dim lines() As String
        Try
            Dim raw As Byte() = File.ReadAllBytes(filePath)
            ' Strip UTF-8 BOM if present
            If (raw.Length >= 3 AndAlso raw(0) = &HEF AndAlso raw(1) = &HBB AndAlso raw(2) = &HBF) Then
                lines = Encoding.UTF8.GetString(raw, 3, raw.Length - 3).Split({vbCrLf, vbLf}, StringSplitOptions.None)
            Else
                lines = latin1.GetString(raw).Split({vbCrLf, vbLf}, StringSplitOptions.None)
            End If
        Catch ex As Exception
            errMsg = String.Format("Failed to read file: {0}", ex.Message)
            Return False
        End Try

        ' Find header line (first non-empty)
        Dim headerIdx As Integer = -1
        For i As Integer = 0 To lines.Length - 1
            If (Not String.IsNullOrWhiteSpace(lines(i))) Then
                headerIdx = i
                Exit For
            End If
        Next
        If (headerIdx < 0) Then
            errMsg = "CSV is empty."
            Return False
        End If

        Dim headerParts() As String = lines(headerIdx).Split(";"c)
        For i As Integer = 0 To headerParts.Length - 1
            headerParts(i) = headerParts(i).Trim().ToLowerInvariant().Replace("é", "e").Replace("è", "e")
        Next

        ' Map columns by header name (flexible order, FR/EN)
        Dim idxFleet As Integer = -1, idxZone As Integer = -1
        Dim idxStart As Integer = -1, idxEnd As Integer = -1, idxStatus As Integer = -1
        For i As Integer = 0 To headerParts.Length - 1
            Dim h As String = headerParts(i)
            If (h = "flottille" OrElse h = "fleet" OrElse h.Contains("flott") OrElse h = "flottile") Then
                idxFleet = i
            ElseIf (h = "zone" OrElse h = "area" OrElse h.Contains("zone")) Then
                idxZone = i
            ElseIf (h = "debut" OrElse h = "start" OrElse h.Contains("debut") OrElse h = "begin") Then
                idxStart = i
            ElseIf (h = "fin" OrElse h = "end" OrElse h.Contains("fin")) Then
                idxEnd = i
            ElseIf (h = "statut" OrElse h = "status" OrElse h.Contains("statut")) Then
                idxStatus = i
            End If
        Next
        If (idxFleet < 0 OrElse idxZone < 0 OrElse idxStart < 0 OrElse idxEnd < 0 OrElse idxStatus < 0) Then
            errMsg = "Invalid header. Expected columns: flottille/fleet;zone;debut/start;fin/end;statut/status (order flexible, ';' separated)."
            Return False
        End If

        ' Build lookup for zone index
        Dim zoneIndex As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i As Integer = 0 To Me.Zones.Count - 1
            zoneIndex(Me.Zones(i).Name) = i
        Next

        ' Temporary structure to detect conflicts: fleet -> year -> zone -> month -> (lineNo, status)
        ' We will directly populate Me.Vector while checking.
        ' First, clear per-fleet/year matrices but keep zone list. We'll need year range:
        ' We don't yet know years; collect intervals first then ensure.
        Dim pending As New List(Of Tuple(Of String, Integer, Integer, Integer, Integer, eRestrictedAreaStatus, Integer))
        ' (fleet, zoneIdx, startYear, startMonth, endYear, endMonth, status, lineNo) -> expand later
        ' Use a helper to store intervals
        Dim intervals As New List(Of IntervalRecord)

        For lineNo As Integer = headerIdx + 2 To lines.Length ' 1-based line numbers
            Dim idx As Integer = lineNo - 1 ' 0-based
            If (idx < 0 OrElse idx >= lines.Length) Then Continue For
            Dim rawLine As String = lines(idx)
            If (String.IsNullOrWhiteSpace(rawLine)) Then Continue For
            Dim trimmed As String = rawLine.Trim()
            If (trimmed.StartsWith("#")) Then Continue For
            Dim parts() As String = rawLine.Split(";"c)
            ' Allow lines with fewer columns than header (malformed)
            If (parts.Length <= Math.Max(Math.Max(idxFleet, idxZone), Math.Max(idxStart, Math.Max(idxEnd, idxStatus)))) Then
                errMsg = String.Format("Invalid number of columns at line {0}.", lineNo)
                Return False
            End If
            Dim sFleet As String = parts(idxFleet).Trim()
            Dim sZone As String = parts(idxZone).Trim()
            Dim sStart As String = parts(idxStart).Trim()
            Dim sEnd As String = parts(idxEnd).Trim()
            Dim sStatus As String = parts(idxStatus).Trim()

            ' Ignore rows where status is open (as agreed)
            Dim statusLower As String = sStatus.ToLowerInvariant().Replace("é", "e").Replace("è", "e")
            If (statusLower = "ouvert" OrElse statusLower = "open" OrElse statusLower = "2") Then Continue For

            If (Not IsValidStatusString(sStatus)) Then
                errMsg = String.Format("Invalid status '{0}' at line {1}. Expected fermé/closed, navigable/navigation, or ouvert/open.", sStatus, lineNo)
                Return False
            End If
            Dim status As eRestrictedAreaStatus = StatusFromString(sStatus)
            ' Validate fleet
            Dim fleetNorm As String = sFleet.Trim().ToLowerInvariant()
            Dim isFleetValid As Boolean = False
            For Each f As String In FIBEFleetTypes
                If (fleetNorm = f.ToLowerInvariant()) Then
                    fleetNorm = f ' canonical
                    isFleetValid = True
                    Exit For
                End If
            Next
            If (Not isFleetValid) Then
                errMsg = String.Format("Unknown fleet '{0}' at line {1}. Expected one of: {2}.", sFleet, lineNo, String.Join(", ", FIBEFleetTypes))
                Return False
            End If
            ' Validate zone
            Dim zIdx As Integer = -1
            If (Not zoneIndex.TryGetValue(sZone, zIdx)) Then
                errMsg = String.Format("Unknown zone '{0}' at line {1}. Available zones: {2}.", sZone, lineNo, String.Join(", ", zoneIndex.Keys))
                Return False
            End If
            ' Parse dates
            Dim y0 As Integer, m0 As Integer, y1 As Integer, m1 As Integer
            If (Not TryParseMonthYear(sStart, y0, m0)) Then
                errMsg = String.Format("Invalid start date '{0}' at line {1}. Expected MM/YYYY or YYYY-MM.", sStart, lineNo)
                Return False
            End If
            If (Not TryParseMonthYear(sEnd, y1, m1)) Then
                errMsg = String.Format("Invalid end date '{0}' at line {1}. Expected MM/YYYY or YYYY-MM.", sEnd, lineNo)
                Return False
            End If
            Dim v0 As Integer = y0 * 12 + m0
            Dim v1 As Integer = y1 * 12 + m1
            If (v0 > v1) Then
                errMsg = String.Format("Start date {0} is after end date {1} at line {2}.", sStart, sEnd, lineNo)
                Return False
            End If
            intervals.Add(New IntervalRecord With {
                .Fleet = fleetNorm,
                .ZoneIdx = zIdx,
                .StartYear = y0,
                .StartMonth = m0,
                .EndYear = y1,
                .EndMonth = m1,
                .Status = status,
                .LineNo = lineNo
            })
        Next

        ' Now apply intervals to Vector, detecting conflicts
        ' Map: fleet -> year -> (zoneLineKey -> lineNo) for conflict reporting
        Dim cellLine As New Dictionary(Of String, Dictionary(Of Integer, Dictionary(Of Integer, Integer)))(
            StringComparer.OrdinalIgnoreCase)

        ' Prepare year range from intervals to ensure matrices exist
        Dim minYear As Integer = Integer.MaxValue, maxYear As Integer = Integer.MinValue
        For Each r As IntervalRecord In intervals
            If (r.StartYear < minYear) Then minYear = r.StartYear
            If (r.EndYear > maxYear) Then maxYear = r.EndYear
        Next
        If (intervals.Count > 0) Then
            Me.EnsureYearRange(minYear, maxYear, Me.Zones.Count)
        End If

        ' Also ensure at least one year exists if no intervals? No need.

        For Each rec As IntervalRecord In intervals
            Dim y As Integer = rec.StartYear
            Dim m As Integer = rec.StartMonth
            While (y < rec.EndYear OrElse (y = rec.EndYear AndAlso m <= rec.EndMonth))
                Dim mat As Integer()() = Me.GetOrCreateVector(rec.Fleet, y, Me.Zones.Count)
                Dim existing As Integer = mat(rec.ZoneIdx)(m - 1)
                ' If existing is not Open and different from new status -> conflict?
                ' But note matrices are initialized as Open. We need to know if this cell
                ' was already set by a previous interval (non-Open). We track via
                ' dictionaries of line numbers for already-set cells.
                Dim fleetDict As Dictionary(Of Integer, Dictionary(Of Integer, Integer)) = Nothing
                If (Not cellLine.TryGetValue(rec.Fleet, fleetDict)) Then
                    fleetDict = New Dictionary(Of Integer, Dictionary(Of Integer, Integer))
                    cellLine(rec.Fleet) = fleetDict
                End If
                Dim yearDict As Dictionary(Of Integer, Integer) = Nothing
                If (Not fleetDict.TryGetValue(y, yearDict)) Then
                    yearDict = New Dictionary(Of Integer, Integer)
                    fleetDict(y) = yearDict
                End If
                ' Use a combined key: zoneIdx * 12 + (m-1) -> but we need per-zone/month
                ' Simpler: maintain 2D array of line numbers parallel to matrix, initialized 0
                ' We can instead check if mat has been set to non-Open by previous interval:
                ' Since matrices start as Open, if existing <> Open and existing <> rec.Status -> conflict
                ' But what if two intervals set same status overlapping? That's okay (redundant).
                ' So conflict only if existing != Open and existing != newStatus and the cell was
                ' previously written by an interval (we can detect via a shadow map).
                ' We maintain a shadow: if cellLine already has entry for this zone/month -> conflict if statuses differ
                Dim zoneLineKey As Integer = rec.ZoneIdx * 100 + m ' unique per zone/month (100 >12)
                Dim prevLine As Integer = 0
                Dim hasPrev As Boolean = yearDict.TryGetValue(zoneLineKey, prevLine)
                If (hasPrev) Then
                    Dim prevStatus As Integer = mat(rec.ZoneIdx)(m - 1)
                    If (prevStatus <> CInt(rec.Status)) Then
                        errMsg = String.Format("There is a conflict between line {0} and line {1}. Please correct it before re-importing the file", prevLine, rec.LineNo)
                        Return False
                    End If
                    ' Same status overlapping -> ignore duplicate
                Else
                    ' First time this cell is set to non-Open. But what if matrix already has non-Open
                    ' from a previous import that we didn't clear? We cleared via EnsureYearRange which
                    ' created fresh matrices (Open). So okay. For incremental imports without clearing,
                    ' we need to detect conflict with existing non-Open value:
                    If (existing <> CInt(eRestrictedAreaStatus.Open) AndAlso existing <> CInt(rec.Status)) Then
                        ' We don't have previous line number for this case (existing data from before import)
                        ' We report generic conflict with current line
                        errMsg = String.Format("There is a conflict at line {0} with existing data for fleet {1}, zone {2}, {3:00}/{4}. Please correct it before re-importing the file", rec.LineNo, rec.Fleet, Me.Zones(rec.ZoneIdx).Name, m, y)
                        Return False
                    End If
                    mat(rec.ZoneIdx)(m - 1) = CInt(rec.Status)
                    yearDict(zoneLineKey) = rec.LineNo
                End If

                ' Advance month
                m += 1
                If (m > 12) Then
                    m = 1
                    y += 1
                End If
            End While
        Next

        Return True
    End Function

    Private Class IntervalRecord
        Public Fleet As String
        Public ZoneIdx As Integer
        Public StartYear As Integer
        Public StartMonth As Integer
        Public EndYear As Integer
        Public EndMonth As Integer
        Public Status As eRestrictedAreaStatus
        Public LineNo As Integer
    End Class

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Export the current vector as a sparse interval CSV.
    ''' Only non-open intervals are written, coalescing consecutive months with
    ''' the same status for the same fleet/zone. Dates are written as MM/YYYY.
    ''' </summary>
    ''' <param name="filePath">Destination file (latin-1).</param>
    ''' <param name="firstYear">First simulation year (inclusive).</param>
    ''' <param name="lastYear">Last simulation year (inclusive).</param>
    ''' -----------------------------------------------------------------------
    Public Sub ExportSparseCsv(filePath As String, firstYear As Integer, lastYear As Integer)
        Dim latin1 As Encoding = Encoding.GetEncoding(28591)
        Dim sb As New StringBuilder()
        ' Header in French as agreed, with English aliases accepted on import
        sb.AppendLine("flottille;zone;debut;fin;statut")
        If (firstYear > lastYear) Then
            File.WriteAllText(filePath, sb.ToString(), latin1)
            Return
        End If
        For Each fleet As String In FIBEFleetTypes
            Dim fleetKey As String = fleet.ToLowerInvariant()
            Dim yearDict As Dictionary(Of Integer, Integer()()) = Nothing
            If (Not Me.Vector.TryGetValue(fleetKey, yearDict)) Then Continue For
            For zIdx As Integer = 0 To Me.Zones.Count - 1
                Dim zoneName As String = Me.Zones(zIdx).Name
                ' Walk through years/months in order, building intervals
                Dim curStatus As eRestrictedAreaStatus = eRestrictedAreaStatus.Open
                Dim curStartYear As Integer = 0, curStartMonth As Integer = 0
                Dim inInterval As Boolean = False
                For yr As Integer = firstYear To lastYear
                    Dim mat As Integer()() = Nothing
                    Dim hasMat As Boolean = yearDict.TryGetValue(yr, mat)
                    For m As Integer = 1 To nMonths
                        Dim status As eRestrictedAreaStatus = eRestrictedAreaStatus.Open
                        If (hasMat AndAlso mat IsNot Nothing AndAlso zIdx < mat.Length AndAlso mat(zIdx) IsNot Nothing) Then
                            status = CType(mat(zIdx)(m - 1), eRestrictedAreaStatus)
                        End If
                        ' We only export non-open
                        Dim isNonOpen As Boolean = (status <> eRestrictedAreaStatus.Open)
                        If (isNonOpen) Then
                            If (Not inInterval) Then
                                ' Start new interval
                                curStatus = status
                                curStartYear = yr
                                curStartMonth = m
                                inInterval = True
                            ElseIf (status <> curStatus) Then
                                ' Status changed -> close previous interval at previous month
                                Dim prevYr As Integer = yr
                                Dim prevM As Integer = m - 1
                                If (prevM = 0) Then
                                    prevM = 12
                                    prevYr -= 1
                                End If
                                AppendInterval(sb, fleet, zoneName, curStatus, curStartYear, curStartMonth, prevYr, prevM)
                                ' Start new
                                curStatus = status
                                curStartYear = yr
                                curStartMonth = m
                            End If
                            ' Else same status -> continue interval
                        Else
                            ' Status is open -> close any open interval
                            If (inInterval) Then
                                Dim prevYr As Integer = yr
                                Dim prevM As Integer = m - 1
                                If (prevM = 0) Then
                                    prevM = 12
                                    prevYr -= 1
                                End If
                                AppendInterval(sb, fleet, zoneName, curStatus, curStartYear, curStartMonth, prevYr, prevM)
                                inInterval = False
                            End If
                        End If
                    Next
                Next
                ' Close trailing interval at lastYear Dec
                If (inInterval) Then
                    AppendInterval(sb, fleet, zoneName, curStatus, curStartYear, curStartMonth, lastYear, 12)
                End If
            Next
        Next
        File.WriteAllText(filePath, sb.ToString(), latin1)
    End Sub

    Private Shared Sub AppendInterval(sb As StringBuilder, fleet As String, zone As String,
                                      status As eRestrictedAreaStatus,
                                      y0 As Integer, m0 As Integer, y1 As Integer, m1 As Integer)
        Dim sStatus As String
        Select Case status
            Case eRestrictedAreaStatus.Closed
                sStatus = "fermé"
            Case eRestrictedAreaStatus.Navigation
                sStatus = "navigable"
            Case Else
                sStatus = "ouvert"
        End Select
        sb.Append(fleet).Append(";").Append(zone).Append(";")
        sb.AppendFormat("{0:00}/{1}", m0, y0).Append(";")
        sb.AppendFormat("{0:00}/{1}", m1, y1).Append(";")
        sb.AppendLine(sStatus)
    End Sub

#End Region ' Sparse CSV import / export

End Class

''' ---------------------------------------------------------------------------
''' <summary>
''' Restriction status of a zone for a fleet in a given month.
''' </summary>
''' <remarks>Values match the encoding used by diatome
''' (see STATUS_ENCODING in restricted_areas.py).</remarks>
''' ---------------------------------------------------------------------------
Public Enum eRestrictedAreaStatus As Integer
    ''' <summary>Fishing prohibited ("fermé").</summary>
    Closed = 0
    ''' <summary>Navigation only, no fishing ("navigable").</summary>
    Navigation = 1
    ''' <summary>Open to fishing ("ouvert").</summary>
    Open = 2
End Enum
