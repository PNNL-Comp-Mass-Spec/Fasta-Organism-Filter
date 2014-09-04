' This program reads a fasta file and finds the organism info defined in the protein description lines
' It optionally creates a filtered fasta file containing only the proteins of interest
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started September 4, 2014
' Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.

' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
' 
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at 
' http://www.apache.org/licenses/LICENSE-2.0
'
Module modMain

	Public Const PROGRAM_DATE As String = "September 4, 2014"

	Private mInputFilePath As String
	Private mOutputFolderPath As String
	Private mOrganismListFile As String

	Private mCreateProteinToOrganismMapFile As Boolean

	Public Function Main() As Integer

		Dim returnCode As Integer = 0

		Try
			' Set the default values
			mInputFilePath = String.Empty

			mOrganismListFile = String.Empty

			Dim proceed = False
			Dim oParseCommandLine As New clsParseCommandLine
			If oParseCommandLine.ParseCommandLine Then
				If SetOptionsUsingCommandLineParameters(oParseCommandLine) Then proceed = True
			End If

			If Not proceed OrElse
			   oParseCommandLine.NeedToShowHelp OrElse
			   oParseCommandLine.ParameterCount + oParseCommandLine.NonSwitchParameterCount = 0 OrElse
			   mInputFilePath.Length = 0 Then
				ShowProgramHelp()
				returnCode = -1
			Else
				Dim oOrganismFilter = New clsFilterFastaByOrganism()

				With oOrganismFilter

					.CreateProteinToOrganismMapFile = mCreateProteinToOrganismMapFile

					''If Not mParameterFilePath Is Nothing AndAlso mParameterFilePath.Length > 0 Then
					''    .LoadParameterFileSettings(mParameterFilePath)
					''End If
				End With

				Dim success = False
				If String.IsNullOrEmpty(mOrganismListFile) Then
					success = oOrganismFilter.FindOrganismsInFasta(mInputFilePath, mOutputFolderPath)
				Else
					success = oOrganismFilter.FilterFastaByOrganism(mInputFilePath, mOrganismListFile, mOutputFolderPath)
				End If

				If success Then
					returnCode = 0
				Else
					returnCode = -1
				End If

			End If

		Catch ex As System.Exception
			clsFilterFastaByOrganism.ShowErrorMessage("Error occurred in modMain->Main: " & System.Environment.NewLine & ex.Message)
			returnCode = -1
		End Try

		Return returnCode

	End Function

	Private Function GetAppVersion() As String
		Return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() & " (" & PROGRAM_DATE & ")"
	End Function

	Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As clsParseCommandLine) As Boolean
		' Returns True if no problems; otherwise, returns false

		Dim strValue As String = String.Empty
		Dim strValidParameters() As String = New String() {"I", "Org", "O", "Map"}

		Try
			' Make sure no invalid parameters are present
			If objParseCommandLine.InvalidParametersPresent(strValidParameters) Then
				Return False
			Else

				' Query objParseCommandLine to see if various parameters are present
				With objParseCommandLine
					If .RetrieveValueForParameter("I", strValue) Then
						mInputFilePath = strValue
					ElseIf .NonSwitchParameterCount > 0 Then
						mInputFilePath = .RetrieveNonSwitchParameter(0)
					End If

					If .RetrieveValueForParameter("Org", strValue) Then
						mOrganismListFile = strValue
					End If

					If .RetrieveValueForParameter("O", strValue) Then
						mOutputFolderPath = strValue
					End If

					If .IsParameterPresent("Map") Then mCreateProteinToOrganismMapFile = True
				End With

				Return True
			End If

		Catch ex As Exception
			clsFilterFastaByOrganism.ShowErrorMessage("Error parsing the command line parameters: " & System.Environment.NewLine & ex.Message)
		End Try

		Return False

	End Function

	Private Sub ShowProgramHelp()

		Try

			Console.WriteLine("This program reads a fasta file and finds the organism info " +
				  "defined in the protein description lines. It optionally creates " +
				  "a filtered fasta file containing only the proteins of interest.")
			Console.WriteLine()

			Console.WriteLine("Program syntax #1:" & ControlChars.NewLine & System.IO.Path.GetFileName(Reflection.Assembly.GetExecutingAssembly().Location) &
			  " SourceFile.fasta [/O:OutputFolderPath] [/Map]")
			Console.WriteLine()

			Console.WriteLine("Program syntax #2:" & ControlChars.NewLine & System.IO.Path.GetFileName(Reflection.Assembly.GetExecutingAssembly().Location) &
			  " SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputFolderPath]")

			Console.WriteLine()

			Console.WriteLine("The input file name is required")
			Console.WriteLine("Surround the filename with double quotes if it contains spaces")
			Console.WriteLine()
			Console.WriteLine("For the first syntax, will find the organisms present in the fasta file, ")
			Console.WriteLine("creating an OrganismSummary file. Assumes the organism name is defined ")
			Console.WriteLine("by the last set of square brackets in the protein description.")
			Console.WriteLine("")
			Console.WriteLine("Use /Map to also create a file mapping protein name to organism name")
			Console.WriteLine("(filename SourceFasta_ProteinOrganismMap.txt")
			Console.WriteLine()
			Console.WriteLine("For the second syntax, use /Org to specify a text file listing organism names ")
			Console.WriteLine("that should be used for filtering the fasta file. The program will create a ")
			Console.WriteLine("new fasta file that only contains proteins from the organisms of interest")
			Console.WriteLine()
			Console.WriteLine("The OrganismListFile should have one organism name per line")
			Console.WriteLine("Organism names that start with 'RegEx:' will be treated as regular expressions ")
			Console.WriteLine("for matching to protein descriptions. Otherwise, assumes that the protein name ")
			Console.WriteLine("is the text betweeen the last set of square brackets in the protein description")
			Console.WriteLine()
			Console.WriteLine("For both modes, use /O to specify an output folder")
			Console.WriteLine("If /O is missing, the output files will be created in the same folder as the source file")
			Console.WriteLine()
			Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2014")
			Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com")
			Console.WriteLine("Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/")
			Console.WriteLine()

			' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
			System.Threading.Thread.Sleep(750)

		Catch ex As Exception
			clsFilterFastaByOrganism.ShowErrorMessage("Error displaying the program syntax: " & ex.Message)
		End Try

	End Sub

End Module
