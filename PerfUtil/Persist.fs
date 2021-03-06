﻿namespace PerfUtil

    module internal Persist =

        open System
        open System.IO
        open System.Xml.Linq

        let private xn name = XName.Get name

        let testToXml (br : BenchmarkResult) =
            XElement(xn "testResult",
                XAttribute(xn "testId", br.TestId),
                XAttribute(xn "testDate", br.Date),
                XElement(xn "elapsedTime", br.Elapsed.Ticks),
                XElement(xn "cpuTime", br.CpuTime.Ticks),
                XElement(xn "gcDelta",
                    br.GcDelta 
                    |> List.mapi (fun gen delta -> XElement(xn <| sprintf "gen%d" gen, delta)))
                )

        let testOfXml sessionId (xEl : XElement) =
            {
                TestId = xEl.Attribute(xn "testId").Value
                SessionId = sessionId
                Date = xEl.Attribute(xn "testDate").Value |> DateTime.Parse

                Elapsed = xEl.Element(xn "elapsedTime").Value |> int64 |> TimeSpan.FromTicks
                CpuTime = xEl.Element(xn "cpuTime").Value |> int64 |> TimeSpan.FromTicks
                GcDelta =
                    xEl.Element(xn "gcDelta").Elements()
                    |> Seq.map (fun gc -> int gc.Value)
                    |> Seq.toList
            }

        let sessionToXml (tests : TestSession) =
            XElement(xn "testRun",
                XAttribute(xn "id", tests.Id),
                XAttribute(xn "date", tests.Date.ToString()),
                tests.Tests 
                |> Map.toSeq 
                |> Seq.map snd 
                |> Seq.sortBy (fun b -> b.Date) 
                |> Seq.map testToXml)

        let sessionOfXml (xEl : XElement) =
            let id = xEl.Attribute(xn "id").Value
            let date = xEl.Attribute(xn "date").Value |> DateTime.Parse
            let tests = 
                xEl.Elements(xn "testResult") 
                |> Seq.map (testOfXml id) 
                |> Seq.map (fun tr -> tr.TestId, tr)
                |> Map.ofSeq
            {
                Id = id
                Date = date
                Tests = tests
            }

        let sessionsToXml (sessionName : string) (session : TestSession list) =
            XDocument(
                XElement(xn "testSuite",
                    XAttribute(xn "suiteName", sessionName),
                    session |> Seq.sortBy(fun s -> s.Date) |> Seq.map sessionToXml))

        let sessionsOfXml (root : XDocument) =
            let xEl = root.Element(xn "testSuite")
            let name = xEl.Attribute(xn "suiteName").Value
            let sessions =
                xEl.Elements(xn "testRun")
                |> Seq.map sessionOfXml
                |> Seq.toList

            name, sessions


        let sessionToFile name (path : string) (sessions : TestSession list) =
            let doc = sessionsToXml name sessions
            doc.Save(path)
            
        let sessionOfFile (path : string) =
            if File.Exists(path) then
                XDocument.Load(path) |> sessionsOfXml |> snd
            elif not <| Directory.Exists(Path.GetDirectoryName(path)) then
                invalidOp <| sprintf "invalid path '%s'." path
            else
                []