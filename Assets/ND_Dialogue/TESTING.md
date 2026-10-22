# ND Dialogue — Checklist test tay

Checklist này dùng để kiểm tra bằng mắt những gì test tự động không thay được: UI của editor, thao tác chuột/phím, Play mode và giao diện in-game.
Mỗi dòng ghi **Pass** hoặc **Fail** vào cột KQ. Khi Fail, ghi lại hiện tượng và lỗi trong Console vào cột Ghi chú.

**Phiên bản:** Unity 6000.3 · ND_BehaviorTree + ND.GraphUI + ND.Localization + ND.Dialogue (+ ND.Dialogue.NDBT)

## 0. Chuẩn bị

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| P-01 | Mở project, chờ compile xong | Console không có lỗi đỏ | | |
| P-02 | Chạy menu `ND_BehaviorTree/Diagnostics/Run Editor Tests` | File `Library/NDBT/test-results.txt` ghi `failed: 0` | | |
| P-03 | Chạy menu `ND_BehaviorTree/Samples/Build Dialogue Guard Sample` | Scene `Assets/ND_Dialogue/Samples/GuardSample/GuardSample.unity` mở ra. Console có dòng `[Guard Sample] Built in …` và **không** có dòng `does not validate` | | |
| P-04 | Xem thư mục `Assets/ND_Dialogue/Samples/GuardSample` | Có: Locale_en, Locale_vi, GuardSample_Localization, GuardSample_Text, GuardSample_Colors, World_Blackboard, Guard_Blackboard, GuardSample_World, GuardSample_Dialogue, GuardSample.unity | | |
| P-05 | Chọn `Guard_Blackboard` | Parent là `World_Blackboard`. Có 6 key: trust, met, alertness, homePosition, bannerColor, target | | |

## 1. Dialogue Editor: mở và điều hướng

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| E-01 | Double-click `GuardSample_Dialogue` | Cửa sổ "Dialogue: GuardSample_Dialogue" mở. Trên cùng là thanh mục: Conversations · Characters · Variables · Quests · Events · Localization · Settings · World (Blackboard). Dưới là graph, inspector bên phải. **Không còn** sidebar bên trái | | |
| E-02 | Bấm từng mục trên thanh | Mỗi mục chiếm **toàn bộ** vùng dưới thanh. Bấm lại Conversations thì graph quay về | | |
| E-03 | Bấm **▴** ở cuối thanh, rồi bấm vào nút còn lại | Thanh gập còn một dòng (tên mục đang mở); bấm vào thì mở lại | | |
| E-04 | Mục Conversations → dòng thứ hai của thanh: chọn "Merchant" rồi "Gate Guard" | Graph đổi theo, tự frame vừa màn hình. Ô bên cạnh đổi tên conversation (Enter), có "+ New" và "Delete" | | |
| E-05 | Bỏ chọn **Inspector** trên toolbar, rồi chọn lại | Inspector bên phải ẩn (graph rộng ra), rồi hiện lại | | |
| E-06 | Đổi ngôn ngữ trên toolbar `en` → `vi` | Node của Gate Guard hiện tiếng Việt. Node của Merchant vẫn tiếng Anh (text thường, chưa localize) | | |
| E-07 | Bấm **Validate** | Hiện thông báo "No problems found." | | |
| E-08 | Inspector của asset database (Project → chọn database) | Có nút "Open Dialogue Editor". Không hiện danh sách conversations thô | | |

## 2. Graph: thao tác

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| G-01 | Hover một node | Hiện chấm xanh ở góc trên-phải | | |
| G-02 | Kéo chấm xanh thả lên node khác | Thêm một mũi tên. Mũi tên chạm **viền** node đích, không đâm vào giữa | | |
| G-03 | Trong lúc kéo (G-02) | Các node nối được có viền xanh lá. START và node đã nối rồi thì không sáng | | |
| G-04 | Nối A→B rồi B→A | Hai mũi tên tách thành hai làn, không đè lên nhau | | |
| G-05 | Alt+click lên một mũi tên | Link bị xóa | | |
| G-06 | Chuột phải vào nền → New NPC Line / New Player Response / New Passthrough | Tạo đúng loại node: viền xanh / viền cam / nền xám | | |
| G-07 | Chọn một node → **Space** rồi **R** | Tạo câu NPC con, rồi câu trả lời của người chơi, nằm bên phải và tự nối vào node cha | | |
| G-08 | Chuột phải vào node → **Link To… → #n** | Link được tạo. Mục không hợp lệ thì bị xám | | |
| G-09 | Chuột phải vào node → **Link Priority → … → High** | Mũi tên chuyển màu cam, dày hơn | | |
| G-09b | Xem các mũi tên ra từ START (mẫu dựng lại sau bản này) | Mỗi nhánh có chip vàng ◆ (điều kiện của nhánh + điều kiện của câu đích). Rê chuột: chi tiết, có mục "Target line". Mũi tên không có điều kiện có dấu **+** mờ ở giữa | | |
| G-09c | Bấm chip ◆ hoặc dấu **+** | Popup: Priority bằng chữ (nút **?** giải thích), "This branch only", "Target line — needed by every way into it", mỗi phần có **+ Add** và ✕. Sửa xong chip trên mũi tên và node cập nhật ngay | | |
| G-10 | Chọn START + một node → **Delete** | Node bị xóa, START vẫn còn. Link trỏ vào node đã xóa cũng mất | | |
| G-11 | **Ctrl+Z** sau G-10, rồi **Ctrl+Y** | Node và link quay lại, rồi lại mất | | |
| G-12 | Kéo node sang chỗ khác → đóng cửa sổ → mở lại | Node vẫn ở chỗ mới | | |
| G-13 | Kéo khung chọn nhiều node rồi kéo cả nhóm | Cả nhóm di chuyển. Một Ctrl+Z trả về vị trí cũ | | |
| G-14 | Pan (chuột giữa / Alt+kéo), zoom (lăn chuột), **F** | Hoạt động mượt. F frame vùng đang chọn, hoặc toàn bộ khi không chọn gì | | |
| G-15 | Gán một Sprite vào Portrait của emotion Neutral (tab Characters → Guard) | Node của Guard hiện ảnh nhô lên trên mép node. Câu Angry dùng portrait của Angry (nếu có). Portrait localized đổi theo ngôn ngữ đang xem | | |

## 3. Inspector của entry

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| I-01 | Chọn node "Halt!" (Gate Guard) | Speaker = Guard. Text ghi `[en]` hoặc `[vi]`, có nhãn table/key và nút **Unlink** | | |
| I-02 | Sửa text ở `vi`, bấm ra ngoài ô | Node trên graph cập nhật. Đổi về `en` thì text tiếng Anh không đổi | | |
| I-03 | Chọn node "Fresh goods!" (Merchant) | Text ghi `(plain)`, có nút **Localize → table (en)** | | |
| I-04 | Bấm **Localize** ở I-03 | Text chuyển sang `[en]`. Bảng `GuardSample_Text` có thêm key `c2.eN` | | |
| I-05 | Đổi Speaker sang Guard | Dropdown Emotion hiện Neutral / Angry / Happy | | |
| I-06 | Mục **Plays only if…** → **+ Add condition → Variable** → bấm ô biến | Popup tìm kiếm giống "Create Node" của BT: **Characters › Guard › trust…** và **World › player › gold…**, mỗi biến có **chấm màu** theo kiểu (cùng màu key blackboard BT). Gõ để lọc. **Không** có `quest.key`. `Merchant.mood` chỉ xuất hiện **một lần** | | |
| I-07 | Chọn biến xong | Ô biến có chấm màu. Ô giá trị theo kiểu: bool = checkbox, số, Vector = xyz, Color = bảng màu, Object = ô kéo-thả | | |
| I-08 | Đổi tên key trong blackboard | Ô biến hiện `⚠ tên`, chấm xám. Validate báo lỗi | | |
| I-09 | **+ Add condition → Any Of (OR group)** | Khung viền vàng **"ANY of these is true (OR)"**, mỗi điều kiện con một dòng có ✕, nút **"+ or…"**. Không còn "Element 0 / Element 1" | | |
| I-10 | Mục **When it plays, do…** → **+ Add action → Raise Event** | Popup event có `open_gate`, `give_item` | | |
| I-11 | Node có ≥ 2 link → mục **Next (links)** | Mỗi link một thẻ: đích · priority bằng chữ (**First / Before normal / Normal / After normal / Fallback**) · **Conditions…** · ✕. Mục gập "What does priority do?" giải thích. Link có điều kiện hiện chip ◆ | | |
| I-12 | Đổi priority, bấm ✕ | Graph cập nhật theo (màu đường / mất mũi tên) | | |

## 4. Các mục của database (thanh trên cùng)

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| S-01 | Mục **Characters** | Hàng **thẻ nhân vật nằm ngang** (portrait hoặc chữ cái đầu có màu, tên, loại); thẻ đang chọn viền vàng; thẻ **+** ở cuối | | |
| S-02 | Bấm thẻ Guard | Bên dưới: tên "Gate Guard" + nhãn "Blackboard Actor"; các nhóm Identity / Emotions / Attributes / … settings; HelpBox "Attributes come from the keys of 'Guard_Blackboard'" | | |
| S-03 | Thẻ **+** | Menu có "Player", "Character (base)", "Blackboard Actor" (và mọi lớp con khác của Actor) | | |
| S-04 | Thêm "Character (base)" → Attributes → **+** | Mỗi attribute **một dòng**: chấm màu · tên · kiểu ▾ · giá trị · ghi chú. Biến `Character.<tên>` có trong popup biến (I-06) dưới Characters › Character | | |
| S-05 | Emotions của Guard; mở "Per language…" của **Angry** | Mỗi emotion một dòng có ảnh nhỏ, id, portrait, tint. Localized Tint trỏ vào `GuardSample_Colors / guard.angry` (en = đỏ, vi = cam) | | |
| S-06 | Mục **Variables** | Nhóm **World**: key của World_Blackboard, mỗi key một dòng **sửa được** (chấm màu BT, tên, giá trị, kiểu, ✕), nút **+ Add variable** mở popup kiểu key có màu. Nhóm **Character attributes** theo từng nhân vật, nút "Edit →" mở thẻ nhân vật | | |
| S-07 | Sửa tên / giá trị / thêm / xóa một key ở S-06 | Asset `World_Blackboard` đổi theo, Ctrl+Z hoàn tác được. Popup biến (I-06) cập nhật | | |
| S-08 | Mục **Quests** | Mỗi quest một dòng: tên · "starts" · trạng thái ▾ · ✕. Ô "new quest id" + **+ Add quest** tạo key int `quest.<id>` | | |
| S-09 | Mục **Events** | Mỗi event một dòng: ⚡ tên · ý nghĩa tham số · ghi chú; thêm/xóa được | | |
| S-10 | Mục **Settings** | Nhóm Text / World / Runner / Plugins ("Behavior Tree World 1.0.0") / Project (nút mở Project Settings) | | |
| S-11 | Mục **World (Blackboard)** | Chọn blackboard + Change / Stop using; bên dưới sửa trực tiếp Variables và Quests như S-06, S-08 | | |
| S-12 | Đổi tên một nhân vật có dấu chấm (`Mr.X`) | Validate báo "the name contains '.'" | | |

## 5. Play mode: mẫu Guard (giá trị mong đợi tính theo dữ liệu mẫu)

Mở `GuardSample.unity`, bấm **Play**. Đi bằng **WASD/mũi tên**. Lại gần NPC sẽ hiện lời nhắc, bấm **E** để nói chuyện. Guard là capsule xanh dương đứng trước cổng; Merchant là trụ màu cam ở bên trái. Bảng trạng thái bên phải hiện giá trị thật của từng biến. Nút "Gate Guard"/"Merchant" ở góc trên trái dùng để bắt đầu nhanh, không cần đi tới.

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| R-01 | Vào Play, đi về phía Guard | Khi vào tầm (khoảng 2.5 m) hiện "E — Talk to Gate Guard"; đi ra xa thì lời nhắc tắt | | |
| R-01b | Bấm **E** cạnh Guard | Lời nhắc tắt, panel thoại hiện ở dưới. Tên **"Gate Guard"**. Câu "Halt! Nobody passes this gate." chạy từng chữ. Người chơi đứng yên khi bấm WASD | | |
| R-02 | Bấm **Space** khi chữ đang chạy | Hiện hết câu ngay. Bấm Space lần nữa thì sang menu lựa chọn | | |
| R-03 | Menu lựa chọn | "1. I'm looking for work." / "2. Out of my way!". Bảng trạng thái: `Guard.met = true` | | |
| R-04 | Bấm phím **1** | Guard: "Find my old key in the market…". Hội thoại kết thúc, panel ẩn. `quest key = Active`, `Guard.trust = 1` | | |
| R-05 | Nói chuyện lại với Guard (E) | "Still no key? Try the merchant." | | |
| R-06 | Đi tới Merchant, bấm E | Tên "Old Merchant": "Fresh goods! You have **10** gold." | | |
| R-07 | Chọn "1. Buy the old key (5 gold)" | "Here you go. Careful, it's rusty. (mood 1)". Ô Events có `⚡ give_item(old_key)`. `player.gold = 5`, `player.has_old_key = true`, `Merchant.mood = 1` | | |
| R-08 | Mở lại Merchant | Chỉ còn "1. Just looking." (đã có chìa khóa) | | |
| R-09 | Mở lại Gate Guard | "My key! Thank you. The gate is yours." Tên màu **xanh lá** (Happy). Event `open_gate`. **Khối "Gate" trong scene biến mất** | | |
| R-10 | Bảng trạng thái sau R-09 | `quest key = Completed`, `Guard.trust = 3`, `Guard.bannerColor = #33CC55FF` | | |
| R-11 | Mở lại Gate Guard | "Welcome back, friend. (trust 3)" | | |
| R-12 | Thoát Play, Play lại. Ở menu đầu chọn **2. Out of my way!** | "Watch your tone, stranger." Tên màu **đỏ** (Angry, en). `Guard.trust = -1`, `Guard.alertness = 0.9` | | |
| R-13 | Bấm **vi** (góc trên) rồi lặp lại R-12 (Play lại) | Câu thoại tiếng Việt. Tên khi Angry màu **cam** (tint theo vùng) | | |
| R-14 | Ở `vi`, mở Merchant | Câu Merchant vẫn tiếng Anh (text thường, chưa localize) | | |
| R-15 | Thoát Play | Asset `World_Blackboard`/`Guard_Blackboard` **không đổi** giá trị (gold vẫn 10, trust vẫn 0) | | |

## 6. Localization (plugin độc lập)

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| L-01 | Dialogue Editor → mục **Localization** (hoặc chọn `GuardSample_Text`) | Nút "Localize all plain lines → Text Table (n)" và bảng dịch sửa tại chỗ: key × en/vi, tìm kiếm, phân trang | | |
| L-02 | **Export CSV…** rồi mở bằng Excel | Tiếng Việt hiển thị đúng dấu | | |
| L-03 | Sửa một ô trong CSV, đổi tên một key → **Import CSV…** | Giá trị mới vào bảng. Key đổi tên mà câu thoại vẫn trỏ đúng (id giữ nguyên) | | |
| L-04 | Một field `LocalizedString` bất kỳ (ví dụ trên `LocalizeStringEvent`) | Có ô chọn bảng, dropdown key có tìm kiếm, preview theo từng ngôn ngữ | | |
| L-05 | Bảng dịch → mở **Google Sheets** → **Copy push script**. Trong một Google Sheet trống: Extensions → Apps Script → dán → Deploy → New deployment → Web app (Execute as: Me, Who has access: Anyone). Dán URL web app vào **Push URL**, link Sheet vào **Sheet URL** | Nút **↓ Pull**, **Open sheet**, **↑ Push** bật lên | | |
| L-06 | **↑ Push** | Sheet có cột id, key, comment, en, vi; id **không** bị đổi thành dạng 1.2E+18. Console: "pushed … keys" | | |
| L-07 | Sửa một câu `vi` trên Sheet → Share "Anyone with the link: Viewer" → **↓ Pull** | Câu mới vào bảng và lên node khi xem `vi`. Một Ctrl+Z hoàn tác cả lần pull | | |
| L-08 | Pull khi Sheet chưa share | Console báo "not readable without signing in…", bảng không đổi | | |

## 7. AI bridge của Dialogue (ẩn trong settings, giống BT)

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| A-01 | Project Settings → **ND Dialogue** | Chỉ có thông tin chung và mục gập **Advanced**. Chưa có trang AI Bridge | | |
| A-02 | Advanced → tick **Enable AI Bridge** | Xuất hiện trang con **ND Dialogue → AI Bridge** | | |
| A-03 | Trang AI Bridge: đặt mật khẩu chủ, chọn **Read + Write** | State: "Running on 127.0.0.1:39878 (ReadWrite)". Có file `Library/NDDialogue/bridge.json` | | |
| A-04 | Terminal: `dotnet tools/ndbt/bin/ndbt.dll dlg-help`, rồi `dlg-database-list` | In danh sách lệnh, rồi danh sách database | | |
| A-05 | `ndbt dlg-conversation-get --database GuardSample_Dialogue --conversation "Gate Guard"` | Outline: từng câu, người nói, điều kiện, hành động, link | | |
| A-06 | `ndbt dlg-entry-add --database GuardSample_Dialogue --conversation "Gate Guard" --actor Guard --text Hello --from 0` | Node mới hiện ngay trên graph đang mở, nối từ START. Ctrl+Z trong Unity hoàn tác | | |
| A-07 | **Lock now** (hoặc khởi động lại Unity) | Bridge dừng, `bridge.json` biến mất, lệnh `dlg-*` báo "not running" | | |

## 8. Behavior Tree: kiểm tra hồi quy sau khi tách GraphUI

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| B-01 | Mở một Behavior Tree có sẵn | Graph hiện bình thường, port trên/dưới như cũ | | |
| B-02 | Kéo node, kéo dây từ output sang input | Port hợp lệ sáng lên khi kéo. Thả xong thì có dây | | |
| B-03 | Alt+click port / Alt+click dây / double-click dây | Ngắt kết nối / cắt dây / chèn reroute | | |
| B-04 | Pan, zoom, **F**, **H**, Ctrl+A, Esc, Space (tạo node) | Như trước khi refactor | | |
| B-05 | Đổi theme có port Cat / Image / Custom | Port vẽ đúng hình | | |
| B-06 | Gán key cho node trong một tree có blackboard với parent | Danh sách key có cả key của **parent** | | |
| B-07 | Live debug khi Play | Dây chạy hạt sáng, màu theo trạng thái như cũ | | |

## 9. Character Database và Save

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| C-01 | Mục **Characters** → nhóm **Cast** → **+ New Character Database** | Tạo `<db>_Characters.asset` cạnh database và thêm vào danh sách Cast | | |
| C-02 | **Move this database's n character(s) to… → <Character DB>** | Thẻ nhân vật có viền dưới xanh ngọc và nhãn "shared · …". Mọi câu thoại vẫn đúng người nói (ID giữ nguyên) | | |
| C-03 | Tạo Dialogue DB thứ hai, thêm cùng Character DB vào Cast | Thấy cùng nhân vật, cùng ID | | |
| C-04 | Ở DB thứ hai → nhóm **Look in this database** → gán portrait khác cho Angry, đặt "Display name here" | Node và game của DB thứ hai dùng ảnh và tên mới. DB đầu không đổi. ✕ trả về ảnh gốc | | |
| C-05 | Trên Character DB: mở **Template attributes**, thêm `affection` (Int). Cho một nhân vật chọn DB đó làm **Template** | Nhân vật hiện dòng "🔒 affection" **không có nút xoá**, ô giá trị sửa được (nhãn "· template"). Biến `<Tên>.affection` có trong popup biến. Thêm attribute riêng trùng tên thì Validate báo | | |
| C-06 | Sửa giá trị `affection` của nhân vật thành 5, rồi bấm ↺ | Nhãn đổi "· own", khuôn vẫn 0; ↺ trả về giá trị của khuôn. Ctrl+Z hoàn tác được | | |
| C-07 | Double-click `Hogwarts_Characters` (mẫu Malfoy) | Cửa sổ **Character Editor**: mục Characters (thẻ + chi tiết, mục "Looks in dialogue databases" thấy Epilogue đổi Angry/Smug/Pout), Template, Used by (2 database) | | |
| SV-01 | Scene mẫu (Guard hoặc Malfoy Story), Play, nói chuyện vài câu, bấm **Save** (góc trên) | Có file `…/NDDialogue/Saves/debug.json` trong `Application.persistentDataPath` | | |
| SV-02 | Chơi tiếp (đổi biến, quest), rồi **Load** | Biến, quest trở lại như lúc Save (xem bảng trạng thái bên phải) | | |
| SV-03 | **Save** khi đang giữa một node nhiều câu (câu 2/3), thoát Play, Play lại, **Load** | Hội thoại hiện lại đúng câu 2, action của node **không** chạy lại | | |
| SV-04 | Save khi đang ở menu lựa chọn → Load | Menu lựa chọn hiện lại | | |
| SV-05 | Điều kiện **Seen**: + Add condition → **Entry Seen**, chọn "not seen · this conversation · #n" cho câu chào lần đầu, câu kia "seen" | Lần đầu nói câu chào lần đầu, lần sau nói câu kia. Sau Save/Load vẫn nhớ | | |

## 10. Test Dialogue (khung chat thử) và bridge test

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| T-01 | Mở `MalfoyStory_Dialogue` → mục **Test Dialogue** → chọn "1. First Meet" → **▶ Start** | Khung chat: Malfoy bên trái (ảnh, tên, emoji emotion, `#id`), Harry bên phải màu xanh. Bên phải là graph, node đang nói viền xanh lá | | |
| T-02 | Bấm **Space** / Enter, phím **1–9** cho lựa chọn | Câu nhiều dòng hiện từng dòng ("1/3, 2/3…"); lựa chọn là các nút bo tròn | | |
| T-03 | Chọn "Shake her hand" | Dưới bong bóng có các dòng nhỏ: `▸ #15 does: Malfoy.affection += 2`, `✎ Malfoy.affection: -1 → 1` | | |
| T-04 | Bỏ tick **Graph** | Chỉ còn khung chat | | |
| T-05 | Bấm `#id` dưới một bong bóng | Id được copy, ô "from #" nhận id đó, graph cuộn tới node | | |
| T-06 | Chuột phải một node trên graph → **Test from here** | Chuyển sang Test Dialogue và bắt đầu đúng tại node đó | | |
| T-07 | Chương "6. Hallway" chơi nhiều lần với các lựa chọn khác nhau | Lời chào đổi theo annoyance / affection / pride (Angry / Embarrassed / Smug / Neutral) | | |
| T-08 | Mẫu Guard: chơi tới câu có event `open_gate` | Dòng `⚡ open_gate` hiện trong chat, cái cổng trong scene **không** biến mất | | |
| T-09 | **⟲ Reset world** | Chat trống, giá trị về ban đầu, các câu "đã nói" bị quên | | |
| T-10 | Terminal: `ndbt dlg-test-run --database MalfoyStory_Dialogue --conversation "6. Hallway (any time)" --json '{"choices":[2]}'` khi Dialogue Editor đang mở database đó | Chat trong editor tự chạy (where = "live"), lệnh in transcript + giá trị biến | | |

## Chưa nằm trong checklist này
- Quest log, bark, sequencer, importer: chưa làm. Trigger kiểu OnTriggerEnter (vùng chạm) có trong component nhưng mẫu chưa dùng.
- `DialogueActorAgent` (thuộc tính lấy từ blackboard runtime của agent BT): đã có test tự động. Mẫu chưa có agent BT nên chưa kiểm được bằng tay.
