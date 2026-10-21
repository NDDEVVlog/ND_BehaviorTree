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
| E-01 | Double-click `GuardSample_Dialogue` | Cửa sổ "Dialogue: GuardSample_Dialogue" mở, chia ba cột: sidebar, graph, inspector | | |
| E-02 | Bấm nút **Database** trên toolbar hai lần | Sidebar ẩn rồi hiện lại | | |
| E-03 | Tab **Conversations** → chọn "Merchant" rồi "Gate Guard" | Graph đổi theo, tự frame vừa màn hình | | |
| E-04 | Đổi ngôn ngữ trên toolbar `en` → `vi` | Node của Gate Guard hiện tiếng Việt. Node của Merchant vẫn tiếng Anh (text thường, chưa localize) | | |
| E-05 | Bấm **Validate** | Hiện thông báo "No problems found." | | |
| E-06 | Inspector của asset database (Project → chọn database) | Có nút "Open Dialogue Editor". Không hiện danh sách conversations thô | | |

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
| G-09 | Chuột phải vào node → **Link Priority → … → High** | Mũi tên có nhãn "High" | | |
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
| I-06 | **+ Add condition → Variable Condition** → mở dropdown Variable | Có `Guard.trust`, `Guard.met`, `Guard.homePosition`, `Guard.bannerColor`, `Merchant.mood`, `player.gold`… **không** có `quest.key` | | |
| I-07 | Gõ tên biến không tồn tại trong YAML / đổi tên key trong blackboard | Dropdown hiện `⚠ tên (missing)`. Validate báo lỗi | | |
| I-08 | **+ Add action → Raise Event Action** → dropdown Event Name | Có `open_gate`, `give_item` | | |
| I-09 | Thêm action Set Variable với biến `Guard.homePosition` | Ô value nhận `1,2,3`. Validate không báo lỗi. Gõ `1,2` thì Validate báo "not a valid Vector3" | | |
| I-10 | Mục **Links**: đổi priority, bấm ✕ | Graph cập nhật theo | | |

## 4. Sidebar: database

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| S-01 | Tab **Characters** → chọn Guard | Tiêu đề "Character #2 · Blackboard Actor". Có HelpBox "Attributes come from the keys of 'Guard_Blackboard'". Có field Blackboard | | |
| S-02 | **+ Character ▾** | Menu có "Character (base)" và "Blackboard Actor" (cùng mọi lớp con khác của Actor) | | |
| S-03 | Thêm "Character (base)" → thêm Attribute `fear` kiểu Float | Biến `Character.fear` xuất hiện trong dropdown biến (I-06) | | |
| S-04 | Mở rộng emotion **Angry** của Guard | Localized Tint trỏ vào `GuardSample_Colors / guard.angry`. Preview: en = đỏ, vi = cam | | |
| S-05 | Tab **Variables** và **Quests** | Hiện danh sách chỉ đọc kèm ghi chú "come from 'GuardSample_World'" | | |
| S-06 | Tab **Events** | Có open_gate, give_item; thêm/xóa được | | |
| S-07 | Tab **Settings** | Text table, Schema Source, Runner Options. Danh sách Plugins có "Behavior Tree World 1.0.0". Nút "Localize all plain lines" hiện đúng số câu còn text thường | | |
| S-08 | Tab **World (Blackboard)** | Liệt kê Variables (kèm kiểu) và Quests (`key`) của World_Blackboard | | |
| S-09 | Đổi tên một nhân vật có dấu chấm (`Mr.X`) | Validate báo "the name contains '.'" | | |

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
| L-01 | Chọn `GuardSample_Text` | Inspector dạng bảng: key × en/vi, tìm kiếm được, phân trang khi nhiều | | |
| L-02 | **Export CSV…** rồi mở bằng Excel | Tiếng Việt hiển thị đúng dấu | | |
| L-03 | Sửa một ô trong CSV, đổi tên một key → **Import CSV…** | Giá trị mới vào bảng. Key đổi tên mà câu thoại vẫn trỏ đúng (id giữ nguyên) | | |
| L-04 | Một field `LocalizedString` bất kỳ (ví dụ trên `LocalizeStringEvent`) | Có ô chọn bảng, dropdown key có tìm kiếm, preview theo từng ngôn ngữ | | |

## 7. Behavior Tree: kiểm tra hồi quy sau khi tách GraphUI

| ID | Bước làm | Kết quả mong đợi | KQ | Ghi chú |
|---|---|---|---|---|
| B-01 | Mở một Behavior Tree có sẵn | Graph hiện bình thường, port trên/dưới như cũ | | |
| B-02 | Kéo node, kéo dây từ output sang input | Port hợp lệ sáng lên khi kéo. Thả xong thì có dây | | |
| B-03 | Alt+click port / Alt+click dây / double-click dây | Ngắt kết nối / cắt dây / chèn reroute | | |
| B-04 | Pan, zoom, **F**, **H**, Ctrl+A, Esc, Space (tạo node) | Như trước khi refactor | | |
| B-05 | Đổi theme có port Cat / Image / Custom | Port vẽ đúng hình | | |
| B-06 | Gán key cho node trong một tree có blackboard với parent | Danh sách key có cả key của **parent** | | |
| B-07 | Live debug khi Play | Dây chạy hạt sáng, màu theo trạng thái như cũ | | |

## Chưa nằm trong checklist này
- Save/load, quest log, bark, sequencer, importer: chưa làm. Trigger kiểu OnTriggerEnter (vùng chạm) có trong component nhưng mẫu chưa dùng.
- `DialogueActorAgent` (thuộc tính lấy từ blackboard runtime của agent BT): đã có test tự động. Mẫu chưa có agent BT nên chưa kiểm được bằng tay.
