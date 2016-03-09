using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
namespace CalendarHeatmap
{
    /*
     *  {
     *      "id": 336,
     *      "access_event_type_id": 1,"
     *      access_token_event_id": 0,
     *      "reader_name": "",
     *      "token_code": "6400ee4d68",
     *      "granted": 1,
     *      "token_id": 19,
     *      "member_id": 1174,
     *      "time": "0000-00-00 00:00:00",
     *      "created": "2014-08-19 00:25:04"
     *  }   
     */
    class Program
    {
        static void Main(string[] args)
        {
            Entry[] Data = JsonConvert.DeserializeObject<Entry[]>(File.ReadAllText("access_events.json"));
            float[] Calendar = new float[7 * 24];
            List<Visit> AllVisits = new List<Visit>();
            List<Visit> ActiveVisits = new List<Visit>();
            DateTime Cutoff = new DateTime(2015, 1, 1, 0, 0, 0);
            TimeSpan Offset = new TimeSpan(-8, 0, 0);
            TimeSpan DSTOffset = new TimeSpan(-7, 0, 0);
            float MaxValue = -1;

            // Parse date information in log & apply dst correction
            for (int x = 0; x < Data.Length; x++)
            {
                if (!string.IsNullOrWhiteSpace(Data[x].created))
                {
                    Data[x].created_time = DateTime.ParseExact(Data[x].created, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    Data[x].created_time += Data[x].created_time.IsDaylightSavingTime() ? DSTOffset : Offset;
                }
            }

            // Now compile a visit set from the access log
            foreach (Entry E in from Event in Data
                                where Event.created_time > Cutoff && (Event.access_event_type_id == 5 || (Event.token_code != "" && Event.member_id > 0))
                                orderby Event.created_time ascending
                                select Event)
            {
                switch (E.access_event_type_id)
                {
                    case 1: // Swipe In
                        if (ActiveVisits.Exists(x => x.UserId == E.member_id))
                            continue;
                        Visit NewVisit = new Visit();
                        NewVisit.UserId = E.member_id;
                        NewVisit.Start = E.created_time;
                        ActiveVisits.Add(NewVisit);
                        break;
                    case 2:
                        break;
                    case 3: // Swipe Out
                        Visit EndedVisit = ActiveVisits.Find(x => x.UserId == E.member_id);
                        if (EndedVisit != null)
                        {
                            EndedVisit.Duration = E.created_time - EndedVisit.Start;
                            AllVisits.Add(EndedVisit);
                            ActiveVisits.Remove(EndedVisit);
                        }
                        break;
                    case 4:
                        break;
                    case 5: // Door Locked
                        foreach (Visit EndingVisit in ActiveVisits)
                        {
                            EndingVisit.Duration = E.created_time - EndingVisit.Start;
                            AllVisits.Add(EndingVisit);
                        }
                        ActiveVisits.Clear();
                        break;
                }
            }

            Data = null;

            foreach (Visit Visit in from V in AllVisits where V.Duration > new TimeSpan(0, 45, 0) && V.Duration < new TimeSpan(24, 0, 0) select V)
            {
                int Hours = (int)Visit.Duration.TotalHours;
                for (int x = Visit.Start.Hour + (int)Visit.Start.DayOfWeek * 24; Hours > 0; x = ++x % Calendar.Length, Hours--)
                {
                    Calendar[x] += 1;
                    MaxValue = Math.Max(MaxValue, Calendar[x]);
                }
            }

            for (int i = 0; i < 7 * 24; i++)
            {
                Calendar[i] /= MaxValue;
            }

            Bitmap CalendarImage = new Bitmap(800, 700);
            Graphics GDI = Graphics.FromImage(CalendarImage);

            GDI.FillRectangle(Brushes.White, 0, 0, CalendarImage.Width, CalendarImage.Height);

            // Draw header
            GDI.DrawRectangle(Pens.Black, 0, 0, 100, 100);
            GDI.DrawRectangle(Pens.Black, 100, 0, 500, 100);
            GDI.DrawRectangle(Pens.Black, 600, 0, 100, 100);

            // Draw hour colours
            for (int i = 0; i < 7 * 24; i++)
            {
                int x = i / 24;
                int y = i % 24;
                int alpha = (int)(255f * Calendar[i]);
                SolidBrush MyBrush = new SolidBrush(Color.FromArgb(alpha, 255, 0, 0));
                GDI.FillRectangle(MyBrush, x * 100, 100 + y * 25, 100, 25);
            }


            // Draw hour slots
            for (int x = 0; x < 700; x += 100)
            {
                for (int y = 100; y < 700; y += 25)
                {
                    GDI.DrawRectangle(Pens.Black, x, y, 100, 25);
                }
            }

            // Draw hour texts
            for (int i = 0; i < 24; i++)
            {
                GDI.DrawString(string.Format("{0:D2}:00", i), new Font("Agency FB Regular", 14), Brushes.Black, 701, 103 + (i * 25));
            }

            // Draw day texts
            string[] Days = new string[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            for (int i = 0; i < 7; i++)
            {
                GDI.DrawString(Days[i], new Font("Agency FB Regular", 14), Brushes.Black, 25 + 100 * i, 80);
            }
            GDI.DrawRectangle(Pens.Black, 0, 0, 700, 700);

            CalendarImage.Save("Output.png");
        }
    }
}

class Visit
{
    public DateTime Start;
    public TimeSpan Duration;
    public int UserId;
}

class Entry
{
    public string id = "";
    public int access_event_type_id = 0;
    public int access_token_event_id = 0;
    public string reader_name = "";
    public string token_code = "";
    public int granted = 0;
    public int token_id = 0;
    public int member_id = 0;
    public string time = "";
    public DateTime created_time;
    public string created = "";
}