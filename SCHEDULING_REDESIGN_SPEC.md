# מפרט שדרוג מערכת השיבוץ וחישוב העומסים — SmartStudy

## סקירה כללית

מסמך זה מפרט 10 שינויים מהותיים במנוע השיבוץ (`SchedulingService`) ובחישוב העומסים (`StressService`). השינויים מכוונים להפוך את המערכת לחכמה, אישית ומבוססת על העדפות המשתמש.

---

## שינוי 1: שיבוץ מחדש מלא בכל שינוי

### מצב קיים
השיבוץ כבר מוחק TaskEvents ישנים ובונה מחדש, אבל הוא רק מוצא חלונות פנויים — לא מזיז משימות קיימות.

### שינוי נדרש
בכל פעם שנוצרת/מתעדכנת/נמחקת משימה, השיבוץ ירוץ **מאפס על כל המשימות** לפי סדר העדיפות החדש. משימה בעדיפות גבוהה תדחוק משימות בעדיפות נמוכה — לא רק תמצא מקום פנוי.

### שינויים טכניים

**`SchedulingService.cs` — `ScheduleAllTasksAsync`:**
1. מחיקת **כל** ה-TaskEvents האוטומטיים (כבר קיים ✓)
2. מיון משימות לפי ציון עדיפות (כבר קיים ✓)
3. שיבוץ לפי הסדר — המשימה הראשונה מקבלת את הסלוטים הטובים ביותר
4. משימות בעדיפות נמוכה מקבלות מה שנשאר

**`TasksController.cs` — טריגרים אוטומטיים:**
```csharp
// אחרי כל אחד מהפעולות הבאות — הפעלת שיבוץ מחדש:
// POST   /api/tasks          (Create)
// PUT    /api/tasks/{id}     (Update — priority, dueDate, estimatedHours changed)
// DELETE /api/tasks/{id}     (Delete)
// POST   /api/tasks/{id}/complete (Complete)
```

הוסף קריאה ל-`_schedulingService.ScheduleAllTasksAsync(email)` בסוף כל אחת מהפעולות.

**`ExamsController.cs`** — אותו דבר: יצירה/עדכון/מחיקה של מבחן מפעילה שיבוץ מחדש.

---

## שינוי 2: ברירת מחדל — משימה לא מתפצלת + הפסקה של 15 דקות

### מצב קיים
משימה מפוצלת אוטומטית לבלוקים של `maxContinuous` (90 דק') ומשובצת בכל סלוט פנוי.

### שינוי נדרש
**ברירת מחדל**: משימה משובצת כבלוק רציף אחד. אם זמן המשימה חורג מ-`MaxContinuousMinutes`, מוסיפים **הפסקה של 15 דקות** ואז ממשיכים מיד (לא מפצלים לימים שונים).

### דוגמה
משימה של 3 שעות עם `MaxContinuousMinutes = 90`:
```
09:00-10:30  — לימוד (90 דק')
10:30-10:45  — הפסקה (15 דק')
10:45-12:15  — המשך לימוד (90 דק')
```

### שינויים טכניים

**`StudentTask.cs` — שדה חדש:**
```csharp
public bool AllowSplitting { get; set; } = false;  // ברירת מחדל: לא לפצל
```

**`SchedulingService.cs` — לוגיקת שיבוץ חדשה:**
```
לכל משימה:
  if (task.AllowSplitting == false):
    // חפש סלוט רציף אחד שמספיק לכל המשימה (כולל הפסקות)
    totalNeeded = estimatedHours + (floor(estimatedHours / maxContinuousHours) * 0.25)
    חפש חלון פנוי של totalNeeded שעות
    אם נמצא:
      שבץ כבלוק אחד עם הפסקות פנימיות
    אם לא נמצא:
      הוסף ל-UnscheduledTasks עם סיבה "לא נמצא חלון רציף"
  else (AllowSplitting == true):
    // לוגיקה הנוכחית — פיצול לסלוטים
    שבץ בבלוקים של 30 דקות בכל מקום פנוי
```

**הוספת הפסקות ב-TaskEvent:**
```csharp
// במקום ליצור TaskEvent אחד רציף, צור רצף:
var sessions = SplitIntoSessionsWithBreaks(slotFrom, totalHours, maxContinuousMinutes, breakMinutes: 15);
foreach (var session in sessions)
{
    newTaskEvents.Add(new TaskEvent { From = session.From, To = session.To, ... });
}
```

**הפסקות אינן אירועים** — הזמן של ההפסקה פשוט לא משובץ (חלון פנוי של 15 דקות בין הסשנים).

---

## שינוי 3: פיצול משימה רק כאשר המשתמש הגדיר

### מצב קיים
כל משימה מפוצלת אוטומטית.

### שינוי נדרש
שדה `AllowSplitting` ב-`StudentTask` (ברירת מחדל: `false`). המשתמש מגדיר בעת יצירת/עריכת משימה.

### שינויים טכניים

**`CreateTaskDto.cs`:**
```csharp
public bool AllowSplitting { get; set; } = false;
```

**`UpdateTaskDto.cs`:**
```csharp
public bool? AllowSplitting { get; set; }
```

**`TaskDto.cs`:**
```csharp
public bool AllowSplitting { get; set; }
```

**Frontend — טופס יצירת/עריכת משימה:**
הוסף toggle/checkbox:
```
☐ אפשר פיצול המשימה ליותר מיום אחד
```
ברירת מחדל: כבוי.

---

## שינוי 4: ערכי ברירת מחדל נגזרים מהעדפות המשתמש

### מצב קיים
ערכי ברירת מחדל hardcoded בקוד:
```csharp
int dayStart = prefs?.DayStartHour ?? 8;
double maxDaily = prefs?.MaxDailyStudyHours ?? 6.0;
```

### שינוי נדרש
כל ערך ברירת מחדל נשלף מהעדפות המשתמש שהוגדרו באונבורדינג. ה-fallback ל-hardcoded נשאר רק למקרה שלא הושלם אונבורדינג.

### שינויים טכניים

**`SchedulingPreferences.cs` — שדות חדשים:**
```csharp
public int BreakDurationMinutes { get; set; } = 15;          // הפסקה בין סשנים
public double DefaultTaskEstimatedHours { get; set; } = 4.0; // זמן מוערך דיפולטיבי למטלה (ראו שינוי 9)
public double MaxDailyTotalHours { get; set; } = 14.0;       // מקסימום שעות פעילות ביום (ראו שינוי 5)
public double ExamPrepHoursPerDay { get; set; } = 5.0;       // שעות הכנה למבחן ליום (ראו שינוי 10)
public int ExamPrepDays { get; set; } = 3;                   // ימי הכנה למבחן (ראו שינוי 10)
```

**`Onboarding2.html` / `onboarding.js`:**
הוסף שדות נוספים בשלב 2:
- זמן הפסקה (דיפולט 15 דקות)
- מקסימום שעות פעילות כוללות ביום (דיפולט 14)
- שעות הכנה למבחן ליום (דיפולט 5)
- ימי הכנה למבחן (דיפולט 3)

---

## שינוי 5: חישוב עומס יומי כולל (Total Daily Load)

### הבעיה
המערכת הנוכחית מחשבת עומס רק לפי שעות למידה. אבל יום עם משמרת של 8 שעות + 3 שעות למידה = 11 שעות פעילות, שזה עלול להיות עומס גם אם 3 שעות למידה זה מתחת ל-`MaxDailyStudyHours`.

### הפתרון המוצע: שני ציונים משולבים

#### הגדרה חדשה: `MaxDailyTotalHours`
המשתמש מגדיר באונבורדינג: "כמה שעות פעילות כוללות ביום את/ה מסוגל/ת?" (ברירת מחדל: 14 שעות = 24 - 8 שינה - 2 שגרה).

#### נוסחת העומס היומי החדשה:

```
# שלב 1: חישוב שעות לפי קטגוריה
studyHours    = שעות למידה משובצות ביום
workHours     = שעות עבודה ביום  
classHours    = שעות שיעורים ביום
personalHours = שעות אירועים אישיים ביום
totalHours    = studyHours + workHours + classHours + personalHours

# שלב 2: ציון עומס למידה (Study Load)
studyLoad = (studyHours / MaxDailyStudyHours) * 100
# → האם אני לומד/ת יותר מדי?

# שלב 3: ציון עומס כולל (Total Load)  
totalLoad = (totalHours / MaxDailyTotalHours) * 100
# → האם היום שלי עמוס מדי בכלל?

# שלב 4: ציון סופי — המקסימום משניהם
dailyScore = max(studyLoad, totalLoad)
```

#### למה המקסימום?
- יום עם 6 שעות למידה + 0 עבודה: `studyLoad=100%, totalLoad=43%` → **ציון 100** (עומס למידה)
- יום עם 3 שעות למידה + 8 שעות עבודה: `studyLoad=50%, totalLoad=79%` → **ציון 79** (עומס כולל)
- יום עם 2 שעות למידה + 2 שעות עבודה: `studyLoad=33%, totalLoad=29%` → **ציון 33** (הכל תקין)

#### השפעה על השיבוץ:
המנוע לא רק בודק `dailyScheduledHours < maxDaily` אלא גם:
```csharp
var totalDayHours = GetTotalEventHours(day, fixedEvents) + dailyScheduledHours[dayKey];
if (totalDayHours >= prefs.MaxDailyTotalHours) continue; // דלג על יום עמוס
```

### שינויים טכניים

**`SchedulingPreferences.cs`:**
```csharp
public double MaxDailyTotalHours { get; set; } = 14.0;
```

**`StressService.cs` — `GetWeeklyStressAsync`:**
```csharp
// חישוב חדש:
double studyHours = dayTaskEvents.Sum(te => (te.To - te.From).TotalHours);
double otherHours = dayEvents.Where(e => e is not TaskEvent)
                             .Sum(e => (e.To - e.From).TotalHours);
double totalHours = studyHours + otherHours;

double studyLoad = (studyHours / maxDailyStudy) * 100;
double totalLoad = (totalHours / maxDailyTotal) * 100;
double dayScore = Math.Min(100, Math.Max(studyLoad, totalLoad));
```

**`StressScoreDto` — שדות חדשים:**
```csharp
public double StudyLoad { get; set; }     // ציון עומס למידה
public double TotalLoad { get; set; }     // ציון עומס כולל
public double TotalScheduledHours { get; set; } // סה"כ שעות ביום
```

**`DailyWorkloadDto` — שדות חדשים:**
```csharp
public double StudyHours { get; set; }
public double WorkHours { get; set; }
public double ClassHours { get; set; }
public double PersonalHours { get; set; }
public double TotalHours { get; set; }
```

---

## שינוי 6: אירועים שאסור להזיז vs. הצעות

### מצב קיים
כל האירועים הקבועים (שיעורים, עבודה, אישי) נחשבים immovable.

### שינוי נדרש
היררכיית אירועים:

| סוג אירוע | סטטוס | פעולה כשאין מקום |
|------------|--------|------------------|
| למידה למבחן | **Immovable** | לעולם לא מוזז |
| שיעורים (Class) | **Immovable** | לעולם לא מוזז |
| עבודה (Work) | **Protected** | המערכת מציעה להזיז, לא מבצעת |
| אישי (Personal) | **Protected** | המערכת מציעה להזיז, לא מבצעת |
| משימות (Task) | **Flexible** | מוזז אוטומטית בשיבוץ מחדש |

### שינויים טכניים

**`SchedulingResultDto.cs` — שדה חדש:**
```csharp
public List<RelocationSuggestionDto> RelocationSuggestions { get; set; } = new();
```

**DTO חדש:**
```csharp
public class RelocationSuggestionDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = null!;
    public string EventType { get; set; } = null!;       // "Work" | "Personal"
    public DateTime CurrentFrom { get; set; }
    public DateTime CurrentTo { get; set; }
    public string BlockedTaskTitle { get; set; } = null!; // איזו משימה נחסמת
    public string Message { get; set; } = null!;          // הודעה למשתמש
}
```

**`SchedulingService.cs` — לוגיקה חדשה:**
```
לכל משימה שלא נמצא לה מקום (UnscheduledTask):
  1. חפש אירועי עבודה/אישי שחופפים לזמנים שהיו יכולים להתאים
  2. אם נמצאו — הוסף RelocationSuggestion:
     "הזזת [אירוע אישי] מיום ג' 14:00-16:00 תפנה מקום ל-[שם המשימה]"
  3. אל תזיז בעצמך — רק הצע
```

**Frontend — הודעות:**
כאשר `RelocationSuggestions` לא ריק, הצג הודעה למשתמש:
```
⚠️ לא נמצא מקום ל-"מטלה 3 בקורס X"
  💡 הצעה: אם תזיז את "יוגה" מיום ד' 16:00-17:30, יתפנה מקום
  [הזז] [התעלם]
```

---

## שינוי 7: עדיפות למשימות משותפות

### מצב קיים
משימות משותפות (`SharedTask`) לא מקבלות בונוס בשיבוץ.

### שינוי נדרש
משימה המסומנת כמשותפת מקבלת בונוס עדיפות בחישוב הציון, כי קשה יותר לתאם אותן מחדש.

### שינויים טכניים

**`SchedulingService.cs` — שינוי נוסחת העדיפות:**
```csharp
// ציון עדיפות מעודכן:
var isShared = task.SharedTask != null;
var sharedBonus = isShared ? 20 : 0;

var score = (1.0 / daysUntilDue) * 40
          + priorityWeight * 30
          + Math.Min(hours, 10) * 3
          + creditBonus                 // שינוי 8
          + sharedBonus;                // +20 למשימות משותפות
```

**למה 20 נקודות?** מספיק כדי להקדים משימה משותפת על פני משימה רגילה באותה עדיפות, אבל לא מספיק כדי לדרוס משימה דחופה.

---

## שינוי 8: קרדיטים של הקורס משפיעים על העדיפות

### מצב קיים
ציון העדיפות לא מתייחס לקרדיטים.

### שינוי נדרש
קורס עם יותר קרדיטים = המטלות שלו חשובות יותר.

### שינויים טכניים

**`SchedulingService.cs` — שינוי נוסחת העדיפות:**
```csharp
// הוספת בונוס קרדיטים:
var credits = task.Course?.Credits ?? 3;
var creditBonus = credits * 2.5;  // 3 קרדיטים = 7.5, 5 קרדיטים = 12.5

var score = (1.0 / daysUntilDue) * 40
          + priorityWeight * 30
          + Math.Min(hours, 10) * 3
          + creditBonus                 // חדש
          + sharedBonus;                // שינוי 7
```

**נוסחת העדיפות המלאה המעודכנת:**
```
score = (1/daysUntilDue) * 40        — דחיפות (0-400 עבור משימות באיחור)
      + priorityWeight * 30          — עדיפות (30/60/90)
      + min(hours, 10) * 3           — כמות עבודה (0-30)
      + credits * 2.5                — חשיבות הקורס (5-15 טיפוסי)
      + sharedBonus                  — בונוס שיתופי (0 או 20)
```

---

## שינוי 9: זמן מוערך דיפולטיבי למטלה — 4 שעות, ניתן לשינוי בקורס

### מצב קיים
`EstimatedHours` מוגדר ב-null (fallback ל-1 שעה בשיבוץ).

### שינוי נדרש
1. ברירת מחדל גלובלית: **4 שעות** (מוגדר ב-`SchedulingPreferences`)
2. ברירת מחדל לפי קורס: ניתן לשנות בהגדרות הקורס
3. ברגע שיש מספיק מטלות מושלמות (≥2) בקורס — חישוב ML גובר

### היררכיה:
```
ML ratio (≥2 משימות מושלמות בקורס)
  ↓ אם אין
ברירת מחדל של הקורס (DefaultTaskHours)
  ↓ אם לא הוגדר
ברירת מחדל גלובלית (DefaultTaskEstimatedHours = 4.0)
  ↓ אם אין העדפות
Hardcoded fallback = 4.0
```

### שינויים טכניים

**`Course.cs` — שדה חדש:**
```csharp
public double? DefaultTaskEstimatedHours { get; set; }  // null = השתמש בגלובלי
```

**`SchedulingPreferences.cs`:**
```csharp
public double DefaultTaskEstimatedHours { get; set; } = 4.0;
```

**`SchedulingService.cs` — שינוי חישוב שעות:**
```csharp
// עבור כל משימה שאין לה EstimatedHours מפורש:
double GetEffectiveHours(StudentTask task, Dictionary<int, double> courseRatios,
                         SchedulingPreferences prefs)
{
    double baseHours;

    if (task.EstimatedHours.HasValue && task.EstimatedHours > 0)
    {
        baseHours = (double)task.EstimatedHours;
    }
    else
    {
        // היררכיית ברירת מחדל
        baseHours = task.Course?.DefaultTaskEstimatedHours
                    ?? prefs?.DefaultTaskEstimatedHours
                    ?? 4.0;
    }

    // התאמת ML אם קיימת
    if (courseRatios.TryGetValue(task.CourseId, out var ratio))
        baseHours *= ratio;

    return baseHours;
}
```

**`UpdateCourseDto.cs` — שדה חדש:**
```csharp
public double? DefaultTaskEstimatedHours { get; set; }
```

**Frontend — הגדרות קורס:**
הוסף שדה בעריכת קורס:
```
זמן מוערך דיפולטיבי למטלה: [___] שעות
(ישמש עד שיהיו מספיק מטלות לחישוב אוטומטי)
```

---

## שינוי 10: הגדרת זמן הכנה למבחן — גלובלי + לפי קורס

### מצב קיים
Hardcoded: 15 שעות (5 שעות × 3 ימים) לכל מבחן.

### שינוי נדרש
1. **באונבורדינג**: המשתמש מגדיר ערכים גלובליים:
   - שעות הכנה ליום (`ExamPrepHoursPerDay`, דיפולט: 5)
   - ימי הכנה (`ExamPrepDays`, דיפולט: 3)
2. **בהגדרות קורס**: ניתן לדרוס את הערכים הגלובליים

### היררכיה:
```
הגדרת הקורס (Course.ExamPrepHoursPerDay + Course.ExamPrepDays)
  ↓ אם לא הוגדר
הגדרה גלובלית (SchedulingPreferences.ExamPrepHoursPerDay + ExamPrepDays)
  ↓ אם אין
Hardcoded: 5 שעות × 3 ימים
```

### שינויים טכניים

**`Course.cs` — שדות חדשים:**
```csharp
public double? ExamPrepHoursPerDay { get; set; }  // null = גלובלי
public int? ExamPrepDays { get; set; }             // null = גלובלי
```

**`SchedulingPreferences.cs`:**
```csharp
public double ExamPrepHoursPerDay { get; set; } = 5.0;
public int ExamPrepDays { get; set; } = 3;
```

**`SchedulingService.cs` — שינוי יצירת משימות הכנה:**
```csharp
foreach (var exam in exams)
{
    // קבלת ערכים מהקורס או מהגלובלי
    var prepHoursPerDay = exam.Course?.ExamPrepHoursPerDay
                          ?? prefs?.ExamPrepHoursPerDay ?? 5.0;
    var prepDays = exam.Course?.ExamPrepDays
                   ?? prefs?.ExamPrepDays ?? 3;
    var totalPrepHours = prepHoursPerDay * prepDays;

    existingStudyTask = new StudentTask
    {
        ...
        EstimatedHours = (decimal)totalPrepHours,
        ...
    };

    // שיבוץ ימי הכנה
    for (int d = 1; d <= prepDays; d++)
    {
        var prepDay = exam.Date.Date.AddDays(-d);
        // שיבוץ prepHoursPerDay שעות ליום
    }
}
```

**`Onboarding2.html`:**
```html
<div class="form-group">
    <label>כמה שעות הכנה למבחן ליום?</label>
    <input type="number" id="examPrepHoursPerDay" value="5" min="1" max="12">
</div>
<div class="form-group">
    <label>כמה ימים לפני המבחן להתחיל הכנה?</label>
    <input type="number" id="examPrepDays" value="3" min="1" max="14">
</div>
```

**`UpdateCourseDto.cs`:**
```csharp
public double? ExamPrepHoursPerDay { get; set; }
public int? ExamPrepDays { get; set; }
```

---

## סיכום נוסחת העדיפות המלאה (אחרי כל השינויים)

```
score = (1 / daysUntilDue) * 40            — דחיפות (משימה באיחור = 1/0.1 = 400 נקודות)
      + priorityWeight * 30                — עדיפות משתמש (High=90, Medium=60, Low=30)
      + min(effectiveHours, 10) * 3        — היקף (0-30 נקודות)
      + credits * 2.5                      — חשיבות הקורס (5-15 טיפוסי)
      + (isShared ? 20 : 0)               — בונוס משימה משותפת
```

### דוגמאות חישוב:

| משימה | deadline | עדיפות | שעות | קרדיטים | משותפת | ציון |
|-------|----------|--------|------|---------|--------|------|
| מטלה A | מחר (1 יום) | High | 4h | 5 | כן | 40+90+12+12.5+20 = **174.5** |
| מטלה B | עוד 3 ימים | High | 6h | 3 | לא | 13.3+90+18+7.5+0 = **128.8** |
| מטלה C | עוד שבוע | Medium | 2h | 4 | כן | 5.7+60+6+10+20 = **101.7** |
| מטלה D | עוד שבוע | Low | 3h | 3 | לא | 5.7+30+9+7.5+0 = **52.2** |

---

## סיכום שינויי DB Schema

### טבלת `SmartStudy_SchedulingPreferences` — שדות חדשים:
```sql
ALTER TABLE SmartStudy_SchedulingPreferences ADD
    BreakDurationMinutes INT NOT NULL DEFAULT 15,
    DefaultTaskEstimatedHours FLOAT NOT NULL DEFAULT 4.0,
    MaxDailyTotalHours FLOAT NOT NULL DEFAULT 14.0,
    ExamPrepHoursPerDay FLOAT NOT NULL DEFAULT 5.0,
    ExamPrepDays INT NOT NULL DEFAULT 3;
```

### טבלת `SmartStudy_Courses` — שדות חדשים:
```sql
ALTER TABLE SmartStudy_Courses ADD
    DefaultTaskEstimatedHours FLOAT NULL,
    ExamPrepHoursPerDay FLOAT NULL,
    ExamPrepDays INT NULL;
```

### טבלת `SmartStudy_Tasks` — שדה חדש:
```sql
ALTER TABLE SmartStudy_Tasks ADD
    AllowSplitting BIT NOT NULL DEFAULT 0;
```

---

## סיכום שינויי קבצים

| קובץ | שינויים |
|-------|---------|
| `Models/SchedulingPreferences.cs` | 5 שדות חדשים |
| `Models/Course.cs` | 3 שדות חדשים |
| `Models/StudentTask.cs` | שדה `AllowSplitting` |
| `DTOs/SchedulingDtos.cs` | `RelocationSuggestionDto` + שדות ב-`DailyWorkloadDto` |
| `DTOs/TaskDtos.cs` | `AllowSplitting` ב-Create/Update/TaskDto |
| `DTOs/CourseDtos.cs` | שדות exam prep + default hours |
| `DTOs/DashboardDtos.cs` | שדות `StudyLoad`, `TotalLoad` ב-StressScoreDto |
| `Services/SchedulingService.cs` | שינוי מהותי — לוגיקת שיבוץ רציף, הפסקות, הצעות הזזה |
| `Services/StressService.cs` | חישוב עומס כולל חדש |
| `Controllers/TasksController.cs` | טריגר שיבוץ מחדש אחרי CRUD |
| `Controllers/ExamsController.cs` | טריגר שיבוץ מחדש אחרי CRUD |
| `Controllers/CoursesController.cs` | שדות חדשים ב-Update |
| `Data/SmartStudyDbContext.cs` | הוספת השדות החדשים למיגרציה |
| `Front/Pages/Onboarding2.html` | שדות חדשים באונבורדינג |
| `Front/Script/modules/onboarding.js` | טיפול בשדות חדשים |
| `Front/CSS/app.css` | עיצוב הצעות הזזה |

---

## סדר ביצוע מומלץ

### שלב 1: תשתית (DB + Models)
1. הוסף שדות ל-`SchedulingPreferences`, `Course`, `StudentTask`
2. עדכן `SmartStudyDbContext` + מיגרציה
3. עדכן DTOs

### שלב 2: מנוע שיבוץ
4. שכתב `ScheduleAllTasksAsync` עם לוגיקה רציפה + הפסקות
5. הוסף `GetEffectiveHours` עם היררכיית ברירת מחדל
6. עדכן נוסחת עדיפות (קרדיטים + שיתופי)
7. הוסף לוגיקת הצעות הזזה
8. הוסף טריגרים אוטומטיים ב-Controllers

### שלב 3: חישוב עומסים
9. שכתב `GetStressScoreAsync` עם Total Load
10. שכתב `GetWeeklyStressAsync` עם חישוב כפול

### שלב 4: Frontend
11. עדכן אונבורדינג (שדות חדשים)
12. עדכן טופס משימה (AllowSplitting)
13. עדכן הגדרות קורס (exam prep + default hours)
14. הוסף UI להצעות הזזה
15. עדכן דשבורד להציג עומס כולל
