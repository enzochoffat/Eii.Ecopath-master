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
''' A single FIBE agent definition (initial situation, simulation step 1).
''' Each row of the "Agents (FIBE)" grid and each line of the import CSV
''' maps to one instance of this class.
''' </summary>
''' ---------------------------------------------------------------------------
Public Class cFibeAgent

    ''' <summary>Canonical fleet name: archipelago, coastal or trawler (lowercase).</summary>
    Public Property Flottille As String = ""

    ''' <summary>Unique display name of the agent (used as FisherAgent name).</summary>
    Public Property Name As String = ""

    ''' <summary>Port index into the FIBE ports map (zero-based).</summary>
    Public Property PortNumber As Integer = 0

    ''' <summary>Habitats assigned to this agent (may be empty).</summary>
    Public Property Habitats As List(Of String) = New List(Of String)

    ''' <summary>Wave height threshold above which the agent stays home.</summary>
    Public Property WaveThreshold As Double = 2.0

    Public Overrides Function ToString() As String
        Return String.Format("{0}:{1} [port={2}]", Me.Flottille, Me.Name, Me.PortNumber)
    End Function

End Class

''' ---------------------------------------------------------------------------
''' <summary>
''' Container for the FIBE initial agents configuration.
''' </summary>
''' <remarks>
''' <para>Holds the per-agent list edited in the "Agents (FIBE)" tab.
''' The list is persisted in the EwE model as JSON and exported at
''' simulation step 1 to <c>Couplage/Data/fibe_agents.json</c> in an
''' aggregated per-fleet form that <c>CreateJSON.ps1</c> merges into
''' the FIBE <c>config.json</c> agents section.</para>
''' <para>Aggregation rules (agreed): counts = row counts per fleet,
''' names ordered archipelago → coastal → trawler blocks (required by
''' <c>loader.py get_model_params</c> slicing), ports in grid order per
''' fleet, habitats = deduplicated union per fleet (order preserved),
''' wave threshold = minimum per fleet (precautionary).</para>
''' </remarks>
''' ---------------------------------------------------------------------------
Public Class cFibeAgentsConfig

    ''' <summary>Fleet types used by FIBE (canonical, lowercase singular).</summary>
    Public Shared ReadOnly FIBEFleetTypes() As String = {"archipelago", "coastal", "trawler"}

    ''' <summary>Default wave threshold applied when a fleet has no agents.</summary>
    Public Const DefaultWaveThreshold As Double = 2.0

    ''' <summary>The per-agent definitions in grid order.</summary>
    Public Property Agents As List(Of cFibeAgent) = New List(Of cFibeAgent)

#Region " Canonicalization "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Normalize a raw fleet string to its canonical form.
    ''' Accepts plurals as aliases (archipelagos, coastals, trawlers).
    ''' </summary>
    ''' <param name="raw">Raw fleet cell value.</param>
    ''' <returns>Canonical name, or empty string when unknown.</returns>
    ''' -----------------------------------------------------------------------
    Public Shared Function CanonicalFleet(raw As String) As String
        If (String.IsNullOrWhiteSpace(raw)) Then Return ""
        Dim t As String = raw.Trim().ToLowerInvariant()
        Select Case t
            Case "archipelago", "archipelagos", "archipelagoes"
                Return "archipelago"
            Case "coastal", "coastals"
                Return "coastal"
            Case "trawler", "trawlers"
                Return "trawler"
            Case Else
                Return ""
        End Select
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>Check whether a fleet name is a known FIBE fleet (aliases included).</summary>
    ''' -----------------------------------------------------------------------
    Public Shared Function IsKnownFleet(raw As String) As Boolean
        Return Not String.IsNullOrEmpty(CanonicalFleet(raw))
    End Function

#End Region ' Canonicalization

#Region " JSON persistence (per-agent, stored in .ewemdb) "

    Private Class AgentDto
        Public Property flottille As String
        Public Property name As String
        Public Property port As Integer
        Public Property habitats As List(Of String)
        Public Property wave As Double
    End Class

    ''' -----------------------------------------------------------------------
    ''' <summary>Serialize the per-agent list to JSON for model persistence.</summary>
    ''' -----------------------------------------------------------------------
    Public Function Serialize() As String
        Dim dtos As New List(Of AgentDto)
        For Each a As cFibeAgent In Me.Agents
            dtos.Add(New AgentDto With {
                .flottille = a.Flottille,
                .name = a.Name,
                .port = a.PortNumber,
                .habitats = New List(Of String)(a.Habitats),
                .wave = a.WaveThreshold
            })
        Next
        Return JsonConvert.SerializeObject(dtos, Formatting.None)
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>Restore the per-agent list from JSON (empty/malformed → empty list).</summary>
    ''' -----------------------------------------------------------------------
    Public Sub Deserialize(strJSON As String)
        Me.Agents = New List(Of cFibeAgent)
        If (String.IsNullOrWhiteSpace(strJSON)) Then Return
        Try
            Dim dtos As List(Of AgentDto) = JsonConvert.DeserializeObject(Of List(Of AgentDto))(strJSON)
            If (dtos Is Nothing) Then Return
            For Each d As AgentDto In dtos
                Dim fleet As String = CanonicalFleet(If(d.flottille, ""))
                If (String.IsNullOrEmpty(fleet)) Then Continue For
                Dim nm As String = If(d.name, "").Trim()
                If (String.IsNullOrEmpty(nm)) Then Continue For
                Dim habs As New List(Of String)
                If (d.habitats IsNot Nothing) Then
                    For Each h As String In d.habitats
                        If (Not String.IsNullOrWhiteSpace(h)) Then habs.Add(h.Trim())
                    Next
                End If
                Dim port As Integer = Math.Max(0, d.port)
                Dim wave As Double = If(d.wave > 0, d.wave, DefaultWaveThreshold)
                Me.Agents.Add(New cFibeAgent With {
                    .Flottille = fleet,
                    .Name = nm,
                    .PortNumber = port,
                    .Habitats = habs,
                    .WaveThreshold = wave
                })
            Next
        Catch
            Me.Agents = New List(Of cFibeAgent)
        End Try
    End Sub

#End Region ' JSON persistence

#Region " Counts "

    ''' -----------------------------------------------------------------------
    ''' <summary>Return the agent count per canonical fleet.</summary>
    ''' -----------------------------------------------------------------------
    Public Function GetCounts() As Dictionary(Of String, Integer)
        Dim counts As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For Each f As String In FIBEFleetTypes
            counts(f) = 0
        Next
        For Each a As cFibeAgent In Me.Agents
            Dim key As String = a.Flottille.Trim().ToLowerInvariant()
            If (counts.ContainsKey(key)) Then counts(key) += 1
        Next
        Return counts
    End Function

#End Region ' Counts

#Region " Aggregated export (per-fleet, consumed by CreateJSON.ps1) "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Build the aggregated per-fleet JSON object written to
    ''' <c>fibe_agents.json</c>. Names are ordered archipelago → coastal →
    ''' trawler blocks to match <c>loader.py</c> slicing.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Function ToFibeJson() As JObject
        Dim counts As Dictionary(Of String, Integer) = Me.GetCounts()
        Dim names As New JArray()
        Dim portsByFleet As New Dictionary(Of String, JArray)(StringComparer.OrdinalIgnoreCase)
        Dim habsByFleet As New Dictionary(Of String, JArray)(StringComparer.OrdinalIgnoreCase)
        Dim waveByFleet As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase)
        For Each f As String In FIBEFleetTypes
            portsByFleet(f) = New JArray()
            habsByFleet(f) = New JArray()
            waveByFleet(f) = DefaultWaveThreshold
        Next

        ' Collect per-fleet rows preserving grid order within each fleet
        Dim seenHabs As New Dictionary(Of String, Dictionary(Of String, Boolean))(StringComparer.OrdinalIgnoreCase)
        Dim minWave As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase)
        For Each f As String In FIBEFleetTypes
            seenHabs(f) = New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)
            minWave(f) = Double.MaxValue
        Next

        For Each fleet As String In FIBEFleetTypes
            For Each a As cFibeAgent In Me.Agents
                If (Not String.Equals(a.Flottille.Trim(), fleet, StringComparison.OrdinalIgnoreCase)) Then Continue For
                names.Add(a.Name)
                portsByFleet(fleet).Add(a.PortNumber)
                If (a.WaveThreshold > 0 AndAlso a.WaveThreshold < minWave(fleet)) Then
                    minWave(fleet) = a.WaveThreshold
                End If
                For Each h As String In a.Habitats
                    Dim t As String = If(h, "").Trim()
                    If (String.IsNullOrEmpty(t)) Then Continue For
                    If (Not seenHabs(fleet).ContainsKey(t)) Then
                        seenHabs(fleet)(t) = True
                        habsByFleet(fleet).Add(t)
                    End If
                Next
            Next
        Next

        Dim json As New JObject()
        Dim numAgents As New JObject()
        numAgents("num_archipelago") = counts("archipelago")
        numAgents("num_coastal") = counts("coastal")
        numAgents("num_trawler") = counts("trawler")
        json("num_agents") = numAgents
        json("names") = names
        json("archipelago_ports") = portsByFleet("archipelago")
        json("coastal_ports") = portsByFleet("coastal")
        json("trawler_ports") = portsByFleet("trawler")
        json("archipelago_habitats") = habsByFleet("archipelago")
        json("coastal_habitats") = habsByFleet("coastal")
        json("trawler_habitats") = habsByFleet("trawler")
        ' NOTE: config.json uses the historical plural key for archipelago wave height.
        For Each f As String In FIBEFleetTypes
            Dim w As Double = DefaultWaveThreshold
            If (counts(f) > 0 AndAlso minWave(f) <> Double.MaxValue) Then w = minWave(f)
            waveByFleet(f) = w
        Next
        json("archipelagos_wave_height") = waveByFleet("archipelago")
        json("coastal_wave_height") = waveByFleet("coastal")
        json("trawler_wave_height") = waveByFleet("trawler")
        Return json
    End Function

#End Region ' Aggregated export

#Region " CSV import / export "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Map a header cell to a column role. Accepts FR/EN variants,
    ''' case-insensitive.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Private Shared Function HeaderRole(h As String) As String
        If (String.IsNullOrWhiteSpace(h)) Then Return ""
        Dim t As String = h.Trim().ToLowerInvariant()
        t = t.Replace("_", " ").Replace("-", " ")
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", " ").Trim()
        If (t = "flottille" OrElse t = "fleet" OrElse t = "flotilla" OrElse t = "flottile" OrElse t.Contains("flott") OrElse t = "fleet type") Then Return "fleet"
        If (t = "name" OrElse t = "nom" OrElse t = "agent" OrElse t = "agent name") Then Return "name"
        If (t = "port number" OrElse t = "port" OrElse t = "port index" OrElse t = "portnumber") Then Return "port"
        If (t = "habitats" OrElse t = "habitat" OrElse t = "habitats list") Then Return "habitats"
        If (t.Contains("wave") OrElse t.Contains("vague") OrElse t.Contains("seuil")) Then Return "wave"
        Return ""
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>Parse a wave threshold cell (accepts 2 / 2.0 / 2,5).</summary>
    ''' -----------------------------------------------------------------------
    Private Shared Function TryParseWave(s As String, ByRef value As Double) As Boolean
        value = 0
        If (String.IsNullOrWhiteSpace(s)) Then Return False
        Dim t As String = s.Trim()
        If (Double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, value) AndAlso value > 0) Then Return True
        ' French decimal comma fallback (column separator is ';' so unambiguous)
        Dim dotted As String = t.Replace(",", ".")
        If (Double.TryParse(dotted, NumberStyles.Float, CultureInfo.InvariantCulture, value) AndAlso value > 0) Then Return True
        Return False
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Import a per-agent CSV. Expected header (order flexible,
    ''' case-insensitive, ';' separated):
    ''' flottille;name;port number;habitats;wave height threshold.
    ''' Habitats cell holds a comma-separated list and may be empty.
    ''' On success the current list is REPLACED.
    ''' </summary>
    ''' <param name="filePath">Path to the CSV file (latin-1 or UTF-8).</param>
    ''' <param name="errMsg">Error message on failure, with line numbers.</param>
    ''' <returns>True on success.</returns>
    ''' -----------------------------------------------------------------------
    Public Function ImportCsv(filePath As String, ByRef errMsg As String) As Boolean
        errMsg = ""
        If (Not File.Exists(filePath)) Then
            errMsg = String.Format("File not found: {0}", filePath)
            Return False
        End If
        Dim latin1 As Encoding = Encoding.GetEncoding(28591)
        Dim lines() As String
        Try
            Dim raw As Byte() = File.ReadAllBytes(filePath)
            If (raw.Length >= 3 AndAlso raw(0) = &HEF AndAlso raw(1) = &HBB AndAlso raw(2) = &HBF) Then
                lines = Encoding.UTF8.GetString(raw, 3, raw.Length - 3).Split({vbCrLf, vbLf}, StringSplitOptions.None)
            Else
                lines = latin1.GetString(raw).Split({vbCrLf, vbLf}, StringSplitOptions.None)
            End If
        Catch ex As Exception
            errMsg = String.Format("Failed to read file: {0}", ex.Message)
            Return False
        End Try

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

        Dim headerLine As String = lines(headerIdx)
        If (Not headerLine.Contains(";")) Then
            errMsg = "Invalid separator. Expected ';' separated columns: flottille;name;port number;habitats;wave height threshold."
            Return False
        End If
        Dim headerParts() As String = headerLine.Split(";"c)
        Dim idxFleet As Integer = -1, idxName As Integer = -1
        Dim idxPort As Integer = -1, idxHabs As Integer = -1, idxWave As Integer = -1
        For i As Integer = 0 To headerParts.Length - 1
            Select Case HeaderRole(headerParts(i))
                Case "fleet" : If (idxFleet < 0) Then idxFleet = i
                Case "name" : If (idxName < 0) Then idxName = i
                Case "port" : If (idxPort < 0) Then idxPort = i
                Case "habitats" : If (idxHabs < 0) Then idxHabs = i
                Case "wave" : If (idxWave < 0) Then idxWave = i
            End Select
        Next
        If (idxFleet < 0 OrElse idxName < 0 OrElse idxPort < 0 OrElse idxHabs < 0 OrElse idxWave < 0) Then
            errMsg = "Invalid header. Expected columns: flottille;name;port number;habitats;wave height threshold (order flexible, ';' separated)."
            Return False
        End If
        Dim maxIdx As Integer = Math.Max(Math.Max(idxFleet, idxName), Math.Max(idxPort, Math.Max(idxHabs, idxWave)))

        Dim imported As New List(Of cFibeAgent)
        Dim seenNames As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        For lineNo As Integer = headerIdx + 2 To lines.Length ' 1-based line numbers
            Dim idx As Integer = lineNo - 1
            If (idx < 0 OrElse idx >= lines.Length) Then Continue For
            Dim rawLine As String = lines(idx)
            If (String.IsNullOrWhiteSpace(rawLine)) Then Continue For
            If (rawLine.Trim().StartsWith("#")) Then Continue For
            Dim parts() As String = rawLine.Split(";"c)
            If (parts.Length <= maxIdx) Then
                errMsg = String.Format("Invalid number of columns at line {0}. Expected at least {1} ';' separated values.", lineNo, maxIdx + 1)
                Return False
            End If
            Dim sFleet As String = parts(idxFleet).Trim()
            Dim sName As String = parts(idxName).Trim()
            Dim sPort As String = parts(idxPort).Trim()
            Dim sHabs As String = parts(idxHabs).Trim()
            Dim sWave As String = parts(idxWave).Trim()

            ' Skip fully empty rows
            If (String.IsNullOrEmpty(sFleet) AndAlso String.IsNullOrEmpty(sName) AndAlso
                String.IsNullOrEmpty(sPort) AndAlso String.IsNullOrEmpty(sHabs) AndAlso
                String.IsNullOrEmpty(sWave)) Then Continue For

            Dim fleet As String = CanonicalFleet(sFleet)
            If (String.IsNullOrEmpty(fleet)) Then
                errMsg = String.Format("Unknown fleet '{0}' at line {1}. Expected one of: {2}.", sFleet, lineNo, String.Join(", ", FIBEFleetTypes))
                Return False
            End If
            If (String.IsNullOrWhiteSpace(sName)) Then
                errMsg = String.Format("Missing agent name at line {0}. Names must be unique and non-empty.", lineNo)
                Return False
            End If
            If (seenNames.ContainsKey(sName)) Then
                errMsg = String.Format("Duplicate agent name '{0}' at line {1} (first seen at line {2}). Names must be unique.", sName, lineNo, seenNames(sName))
                Return False
            End If
            Dim port As Integer
            If (Not Integer.TryParse(sPort, NumberStyles.Integer, CultureInfo.InvariantCulture, port) OrElse port < 0) Then
                errMsg = String.Format("Invalid port number '{0}' at line {1}. Expected an integer >= 0.", sPort, lineNo)
                Return False
            End If
            Dim wave As Double
            If (Not TryParseWave(sWave, wave)) Then
                errMsg = String.Format("Invalid wave height threshold '{0}' at line {1}. Expected a number > 0.", sWave, lineNo)
                Return False
            End If
            Dim habs As New List(Of String)
            If (Not String.IsNullOrEmpty(sHabs)) Then
                For Each h As String In sHabs.Split(","c)
                    Dim t As String = If(h, "").Trim()
                    If (Not String.IsNullOrEmpty(t)) Then
                        ' Deduplicate within the row (case-insensitive)
                        Dim exists As Boolean = False
                        For Each e As String In habs
                            If (String.Equals(e, t, StringComparison.OrdinalIgnoreCase)) Then
                                exists = True
                                Exit For
                            End If
                        Next
                        If (Not exists) Then habs.Add(t)
                    End If
                Next
            End If

            seenNames(sName) = lineNo
            imported.Add(New cFibeAgent With {
                .Flottille = fleet,
                .Name = sName,
                .PortNumber = port,
                .Habitats = habs,
                .WaveThreshold = wave
            })
        Next

        Me.Agents = imported
        Return True
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Export the current list as a per-agent CSV (latin-1).
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Sub ExportCsv(filePath As String)
        Dim latin1 As Encoding = Encoding.GetEncoding(28591)
        Dim sb As New StringBuilder()
        sb.AppendLine("flottille;name;port number;habitats;wave height threshold")
        For Each a As cFibeAgent In Me.Agents
            sb.Append(a.Flottille).Append(";")
            sb.Append(a.Name).Append(";")
            sb.Append(a.PortNumber.ToString(CultureInfo.InvariantCulture)).Append(";")
            sb.Append(String.Join(",", a.Habitats.ToArray())).Append(";")
            sb.Append(a.WaveThreshold.ToString(CultureInfo.InvariantCulture))
            sb.AppendLine()
        Next
        File.WriteAllText(filePath, sb.ToString(), latin1)
    End Sub

#End Region ' CSV import / export

End Class
