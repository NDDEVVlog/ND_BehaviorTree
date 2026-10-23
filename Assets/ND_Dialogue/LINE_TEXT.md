# Chữ chạy, markup và voice

Một câu thoại được viết kèm thẻ `<...>` ngay trong text. Khi tới lượt câu đó, hệ thống tự gõ chữ ra, dừng, đổi tốc độ,
gạch rồi sửa chữ, cho chữ rung/lượn sóng và phát event — chỉ dựa vào câu chữ. UI không tự quyết gì: nhận data, chạy.

Game (UI TextMeshPro) và cửa sổ **Test Dialogue** dùng chung một pipeline, nên hiện giống nhau.

---

## 1. Các thẻ

| Thẻ | Tác dụng |
|---|---|
| `<pause 0.5>` (hoặc `<wait=0.5>`) | Dừng gõ 0.5 giây |
| `<speed 15>…</speed>` | Gõ 15 ký tự/giây trong đoạn này (đóng thẻ = về tốc độ mặc định) |
| `<wave>…</wave>` | Chữ lượn sóng |
| `<shake>…</shake>` | Chữ rung |
| `<whisper>…</whisper>` | Chữ nhỏ, xám |
| `<shout>…</shout>` | Chữ to, đậm, rung |
| `<err id="a">…</err>` | Chữ sai: đỏ, gạch ngang, rung |
| `<fix id="a" text="…">` | Tại thời điểm này, chữ của `err id="a"` biến mất và chữ mới hiện **đúng chỗ đó** (xanh) |
| `<br posX="40">` | Xuống dòng, thụt vào 40px |
| `<event name="door" arg="open">` | Khi gõ tới đây thì phát event `door(open)` |
| Thẻ khác: `<b>`, `<i>`, `<color=…>`, `<size=…>`… | Giữ nguyên làm rich text của TextMeshPro |

Ví dụ:
```
SYSTEM <err id="sys">FALURE</err> DETECTED.<br posX="50">ATTEMPTING REBOOT...<pause 0.5><fix id="sys" text="ONLINE">
```
Gõ hết hai dòng, dừng 0.5s, rồi chữ `FALURE` ở **dòng trên** đổi thành `ONLINE`.

> `<fix>` đặt càng xa `</err>` thì càng thấy rõ cảnh "quay lại sửa". Đặt ngay sau `</err>` thì chữ đổi liền.

Lỗi viết thẻ (`<pause>` thiếu số, `<wave>` không đóng, `<fix>` không có `err` cùng id, `</shake>` thừa…) hiện màu đỏ
ngay dưới ô chữ trong Inspector của entry, và có trong **Validate** của database.

---

## 2. Dữ liệu đi qua những bước nào

Theo câu `I <err id="a">hate</err><fix id="a" text="love"> <wave>you</wave>`, tốc độ 10 ký tự/giây:

```
DialogueRunner ──DialogueLine──▶ TypewriterDialogueUI ──Play(text, độ dài voice)──▶ TypewriterText
                                                                                       │
     ┌─────────────────────────────────────────────────────────────────────────────────┘
     ▼
 ① LineMarkup.Parse        chuỗi  → mảng LineCommand            (1 lần / câu)
 ② LineTimeline.Build      lệnh   → các mảnh có giờ hiện/ẩn      (1 lần / câu)
 ③ LineTimeline.Compose(t) giờ t  → chữ tới lúc t + khoảng hiệu ứng (mỗi frame)
 ④ TypewriterText          đưa chữ vào TMP, dời glyph rung/sóng, phát event (mỗi frame)
```

**① Parse** — chỉ tách chữ và thẻ thành danh sách struct, chưa có gì chạy:
`Text "I "` · `StyleOpen err (a)` · `Text "hate"` · `StyleClose err` · `Fix a → "love"` · `Text " "` · `StyleOpen wave` · `Text "you"` · `StyleClose wave`

**② Build** — đi qua các lệnh với một đồng hồ, gán giờ cho từng mảnh:

| Mảnh | Hiện từ | Ẩn từ | Kiểu |
|---|---|---|---|
| `I ` | 0.0s | — | thường |
| `hate` | 0.2s | **0.6s** | err |
| `love` | **0.6s** (cả chữ một lúc) | — | fix |
| ` ` | 0.9s (dừng 0.3s để thấy chữ đổi) | — | thường |
| `you` | 1.0s | — | wave |

Gạch/sửa được giải quyết **ở bước này**: `Fix` tìm lại các mảnh cùng id (dù ở dòng nào), đặt giờ ẩn cho chúng và
chèn chữ mới ngay sau chúng. Lúc chạy không có code nào đi tìm chữ để thay. `<pause>`, `<speed>` chỉ làm đồng hồ
nhảy/đổi nhịp. Có voice thì cả timeline được kéo giãn/co lại cho dài đúng bằng clip.

**③ Compose(t)** — mỗi frame trả về:
- chữ tới thời điểm t (rich text TMP hiểu: màu, gạch ngang, `<indent>`…)
- các khoảng ký tự có hiệu ứng (`ký tự 7–8: Wave`)
- đã gõ xong chưa

Ví dụ `t = 0.35s` → `I <color=#FF6B6B><s>ha</s></color>`; `t = 0.7s` → `I <color=#7CFFB2>love</color>`.

**④ TypewriterText** — phần duy nhất làm animation thật:
1. Chữ khác frame trước → `SetText` vào TMP (TMP tự vẽ màu, gạch ngang), lưu vị trí gốc của các glyph.
2. Mỗi frame, kể cả khi chữ không đổi: dời 4 đỉnh của từng ký tự trong khoảng hiệu ứng —
   Wave = `sin(thời gian + vị trí ký tự)`, Shake = Perlin noise. Tham số Wave Height / Speed, Shake Amount trên Inspector.
3. Gõ tới `<event>` → `EventReached` → UI chuyển sang `DialogueEvents.Raise` (listener trong scene nhận như action RaiseEvent).
4. Tiếng "blip" mỗi N ký tự (tuỳ chọn).

| Việc | Chỗ xử lý |
|---|---|
| Chữ hiện dần, pause, speed | ② tính giờ, ③ cắt chữ theo t |
| Gạch rồi sửa | ②: chỉ là giờ ẩn/hiện của các mảnh |
| Màu, gạch ngang, cỡ chữ | Rich text, TMP tự vẽ |
| Rung, lượn sóng | ④ `TypewriterText.Animate()` |

---

## 3. Trong scene

```
Dialogue Canvas
└─ Dialogue UI (TMP)      TypewriterDialogueUI + AudioSource (voice)
   └─ Panel
      └─ Subtitle
         ├─ Portrait, Speaker
         ├─ Line          TextMeshProUGUI + TypewriterText
         └─ Continue
      └─ Responses        nút lựa chọn
```

Tạo bằng **GameObject › ND Dialogue › Dialogue Manager + UI (TextMeshPro)**. Cần TMP Essential Resources
(Window › TextMeshPro › Import TMP Essential Resources); thiếu font thì menu báo lỗi và không tạo.

`TypewriterDialogueUI`:
- **Continue** (nút, Space, Enter): lần 1 hiện hết câu, lần 2 sang câu sau. Phím 1–9 chọn lựa chọn.
- **Voice Source**: phát voice của câu.
- **Type With Voice**: chữ gõ dài đúng bằng clip.
- **Continue After Voice** + **Continue Delay**: hết voice thì tự sang câu (câu có lựa chọn vẫn chờ).
- **Response Timeout / Timeout Choice**: hết giờ thì tự chọn.

---

## 4. Voice

Mỗi câu (kể cả từng câu trong "more lines") có:
- **Clip** — AudioClip dùng cho mọi ngôn ngữ.
- **Per language** — một entry trong Asset Table; có thì dùng nó thay cho Clip.

Sửa trong Inspector của entry, foldout **🔊 Voice** dưới mỗi câu. Runner đọc qua `DialogueRunner.VoiceResolver`
và gửi xuống trong `DialogueLine.Voice`.

---

## 5. Test Dialogue

- Bật **Typing**: câu gõ ra như trong game (pause, speed, gạch/sửa, dài theo voice). Space lần 1 hiện hết câu.
- Dưới câu có dòng `🔊 voice: tên (2.0 s)` và `⚡ door(open) — in the line, at 0.40 s`.
- Không có rung/sóng: UI Toolkit không dời được từng glyph. Bước ①–③ giống hệt game.
- Event trong câu chỉ được liệt kê, không gửi tới scene.

---

## 6. UI cũ (UI.Text) và bubble bark

`StandardDialogueUI` và `BarkUI` hiện câu đã xử lý xong (`LineTimeline.Final`): không thấy thẻ thô, nhưng không gõ
theo pause/speed và không có hiệu ứng. Với `UI.Text` (không phải TMP), các thẻ nó không vẽ được (`<s>`, `<size=%>`,
`<indent>`) bị bỏ đi; còn lại `<b> <i> <color>`.

---

## 7. Thêm style

Style là một bảng tên → (rich text mở, rich text đóng, hiệu ứng). Thêm khi khởi động game:

```csharp
LineStyles.Register("angry", new LineStyle("<color=#f44><b>", "</b></color>", LineEffect.Shake));
```
Sau đó viết `<angry>…</angry>` trong câu. Hiệu ứng hiện có: `None`, `Wave`, `Shake`.

---

## 8. Hiệu năng

Không tạo rác (GC) mỗi frame khi chữ đang chạy:
- `Compose` ghi vào buffer dùng lại; chỉ báo `Changed` khi chữ thật sự đổi.
- `TypewriterText` dùng `TMP_Text.SetText(StringBuilder)` (không tạo string) và buffer vị trí glyph dùng lại.

Mỗi câu (không phải mỗi frame) vẫn cấp phát khi parse + dựng timeline.

---

## 9. So với mẫu "Aesthetic Dialogue" (ND_Unity_Lab)

| Mẫu | ND Dialogue |
|---|---|
| `DialogueParser` + `IDialogueCommand` | `LineMarkup.Parse` → `LineCommand` |
| `_errorRegistry` + `ApplyFix()` (tìm lại Label lúc chạy) | `LineTimeline.Fix()` (tìm lại mảnh lúc dựng timeline) |
| USS `style-error` / `style-fix` | `LineStyles` `err` / `fix` |
| Coroutine gõ **từng từ** | Timeline gõ **từng ký tự** |
| `transition: color 0.3s` khi sửa | Chữ sửa hiện ngay, dừng 0.3s (`FixHold`) |

---

## 10. Chưa làm

- Bảng style là code (`LineStyles`); chưa chỉnh được trên Inspector (ScriptableObject style sheet).
- Hiệu ứng là enum (`Wave`, `Shake`); thêm hiệu ứng mới phải sửa `LineEffect` + `TypewriterText.Animate()`.
- Chưa có chế độ gõ theo từ, chưa có chuyển màu dần khi sửa.
- Test Dialogue không có rung/sóng.

## File

| File | Vai trò |
|---|---|
| `Runtime/Text/LineMarkup.cs` | ① Parse thẻ |
| `Runtime/Text/LineStyles.cs` | Bảng style |
| `Runtime/Text/LineTimeline.cs` | ② Build, ③ Compose, `Final` |
| `Runtime/UI/TypewriterText.cs` | ④ Vẽ vào TMP, rung/sóng, event, blip |
| `Runtime/UI/TypewriterDialogueUI.cs` | UI hội thoại TMP, voice, continue |
| `Runtime/UI/UIText.cs` | Ghi chữ cho UI.Text/TMP, lọc thẻ cho UI.Text |
| `Editor/UI/DialogueUIBuilder.Typewriter.cs` | Menu dựng UI TMP |
| `Tests/Editor/LineTimelineTests.cs` | Test pipeline |
