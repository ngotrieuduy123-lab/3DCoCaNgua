# Tài Liệu Bàn Giao Tiến Độ — 3D Cờ Cá Ngựa: Party Clash (v0.2)

> **Mục đích tài liệu:** File này lưu trữ toàn bộ ngữ cảnh thiết kế, danh sách các file code đã thay đổi, các lỗi đã xử lý và lộ trình công việc tiếp theo để tiếp tục phát triển trên máy tính (PC) mới mà không cần lịch sử đoạn chat cũ.

---

## 1. Tóm Tắt Luật Chơi Mới: Party Clash v0.2 (Đã Chốt & Đang Áp Dụng)

Phiên bản **Party Clash v0.2** chuyển đổi Cờ Cá Ngựa truyền thống thành lối chơi **Tính Điểm Tốc Độ Cao (10 phút)** kết hợp **2 Xúc Xắc Độc Lập (Tactical Split-Dice)**:

### 1.1. Khởi tạo trận đấu & Nhịp độ
- **Khởi đầu cân bằng (Anti-First-Turn Advantage):** Mỗi người chơi bắt đầu trận với **2 quân sẵn trên bàn** (1 quân ở ô khởi hành `+0` có khiên bảo vệ vòng đầu, 1 quân ở ô `+3`) và **2 quân trong chuồng**. Người đi đầu tiên được chọn ngẫu nhiên.
- **Luật Hiện Diện Tối Thiểu (Minimum Board Presence = 2):** Nếu đầu lượt người chơi có **ít hơn 2 quân trên bàn** và còn quân hợp lệ trong chuồng, họ **bắt buộc** phải dùng xúc xắc để xuất quân ra bàn (chống chiến thuật giấu quân trong chuồng).
- **Thời gian:**
  - Tổng thời gian trận: **10 phút (600 giây)**. Khi đồng hồ về `00:00`, trận đấu kích hoạt **Vòng Lượt Cuối (Final Round)** để mọi người chơi đều có tổng số lượt bằng nhau trước khi tổng kết điểm.
  - Thời gian suy nghĩ mỗi hành động: **20 giây**.

### 1.2. Cơ chế 2 Xúc Xắc Độc Lập & Xuất Quân Xung Kích
- Mỗi lượt tung **2 viên xúc xắc**, tạo ra **2 hành động độc lập**: `[Viên 1]` và `[Viên 2]`.
- Người chơi tự chọn dùng viên nào trước, có thể dồn cả 2 viên cho **1 quân cờ** hoặc chia cho **2 quân cờ khác nhau**.
- **Không có thêm lượt (No Extra Turn)** khi tung ra đôi hoặc `1-6` (đảm bảo không ai bị ngồi nhìn người khác đi liên tục).
- **Xuất Quân Xung Kích (Rolling Spawn):** Mọi mặt xúc xắc từ `1..6` đều xuất được quân từ chuồng. Dùng viên giá trị `X` sẽ đưa quân ra ô khởi hành (`+0`) rồi đi tiếp `X - 1` bước.
  - Nếu dùng viên `1` (dừng ngay tại ô khởi hành `+0`) mà trùng quân địch: đẩy quân địch về chuồng nhưng **không được cộng điểm đá và không kích hoạt Combo**.
  - Nếu dùng viên `2..6` đi tiếp và đáp trúng quân địch ở các ô ngoài: tính là **Đá chuẩn** (được điểm + kích hoạt Combo).

### 1.3. Giao tranh, Combo Đá & Vượt Mặt
- **Đi xuyên qua mọi quân cờ** trên đường đi (không bị cản đường).
- Chỉ xét tương tác tại **ô dừng chân cuối cùng** của mỗi hành động:
  - Không được dừng chân chồng lên quân cùng màu.
  - Dừng chân trùng ô quân địch $\rightarrow$ **Đá quân địch về chuồng**.
- **Combo Đá (`+3 bước`):** Sau khi thực hiện xong cả 2 viên xúc xắc của lượt, nếu trong lượt đó có ít nhất 1 pha đá quân địch tính điểm, người chơi mở khóa nút hành động thứ 3: **`[Combo +3]`** (cho phép 1 quân đang ở trên bàn đi thêm 3 ô). Tối đa 1 lần Combo mỗi lượt.
- **4 Ô Tăng Tốc (Dash Tiles `+3 bước`):** Đặt cố định tại các ô `tileIndex = 7, 21, 35, 49` (hiển thị vòng sáng màu Cyan). Dừng chân tại Ô Tăng Tốc sẽ trượt thêm **3 bước** ngay lập tức (nếu ô đích `+3` không bị quân nhà chiếm).

### 1.4. Hệ thống Điểm Số & Thu Quân Tức Thì (Instant Cash-in Home)
| Sự kiện | Điểm thưởng | Chi tiết |
| :--- | :---: | :--- |
| **Qua Trạm Kiểm Soát (Checkpoint)** | **+1 điểm** | Khi quân cờ cán mốc bước thứ `28` (`stepsMoved >= 28` — nửa vòng bàn cờ). Quân cờ tiến hóa thành **Quân Tinh Nhuệ (Elite Piece)** có vòng sáng Vàng Kim dưới chân. |
| **Đá Quân Tân Binh (Rookie Kick)** | **+1 điểm** | Đá quân địch chưa qua bước 28 (`stepsMoved < 28`). |
| **Đá Quân Tinh Nhuệ (Elite Bounty)** | **+3 điểm** | Đá quân địch đã qua bước 28 (`stepsMoved >= 28`). |
| **Dấu Ấn Báo Thù (Nemesis Mark)** | **+1 điểm** | Quân vừa đá đối phương sẽ mang vòng sáng Đỏ Thẫm trong 1 vòng lượt. Bất kỳ ai đá hạ quân mang dấu ấn này được cộng thêm `+1 điểm`. |
| **Về Đích Nông (Ô chuồng 1 – 3)** | **+4 điểm** | Khi `stepsMoved + step > 55`, quân cờ lao thẳng vào đường về đích mà không cần đứng chờ trước cửa chuồng. |
| **Về Đích Sâu (Ô chuồng 4 – 6)** | **+6 điểm** | Vào sâu từ bậc 4 đến bậc 6 (nếu dư bước ở bậc 6 sẽ tự dừng tại bậc 6). |

- **Thu Quân Tái Sử Dụng (Instant Cash-in):** Ngay khi vào đường về đích và nhận `+4` hoặc `+6` điểm, quân cờ **tự động thu về chuồng** để sẵn sàng xuất trận lại từ lượt kế tiếp (không thể xuất lại ngay trong cùng lượt vừa về đích).
- **Sự kiện toàn bản đồ (Global Events — mỗi 3 vòng lượt):**
  - **Săn Tiền Thưởng (Bounty Hunt):** Đá hạ quân thuộc người chơi đang dẫn đầu điểm (`Top 1`) được cộng thêm **+2 điểm**.
  - **Đua Về Đích (Home Rush):** Mọi pha đưa quân vào chuồng đích được cộng thêm **+2 điểm**.
- **Xếp hạng chung cuộc:** Điểm cao nhất thắng (Tie-break: Số lần về đích $\rightarrow$ Số lần đá $\rightarrow$ Tổng bước tiến trên bàn).

---

## 2. Danh Sách Các File Đã Thay Đổi Trong Dự Án

Toàn bộ các thay đổi thuộc **Phase 1 (Core Gameplay + HUD + Network Sync + Bug Fixes)** đã được triển khai và kiểm thử không có lỗi biên dịch (`0 Error(s)`):

### 2.1. Core & Luật Chơi (`Assets/Scripts/Gameplay/` & `Assets/Scripts/Core/`)
1. [`Assets/Scripts/Gameplay/DiceRuleUtility.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Gameplay/DiceRuleUtility.cs)
   - Thêm enum `TurnActionSlot` (`None`, `Dice1`, `Dice2`, `Bonus3`) và `PartyEventType` (`None`, `BountyHunt`, `HomeRush`).
   - Định nghĩa toàn bộ hằng số điểm số, mốc Checkpoint (`28`), vị trí 4 Ô Tăng Tốc (`7, 21, 35, 49`), thời gian lượt (`20s`) và thời gian trận (`600s`).
2. [`Assets/Scripts/Core/BoardTile.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Core/BoardTile.cs)
   - Thêm thuộc tính `IsDashTile` trên các ô bàn cờ.
3. [`Assets/Scripts/Gameplay/PieceController.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Gameplay/PieceController.cs)
   - Thêm trạng thái Party Clash: `hasPassedCheckpoint`, `hasNemesisMark`, `justCashedInThisTurn`, `hasSpawnProtection`, `lastActionPointsAwarded`, `lastActionAwardedScoredKick`.
   - Triển khai `PlaceOnBoardAtOffset` (đặt 2 quân đầu trận), `SpawnAndMoveByDice` (Rolling Spawn), đi xuyên quân, kích hoạt Ô Tăng Tốc (`+3 bước`), và `Instant Cash-in Home` (nhận điểm + thu quân về chuồng).
   - Tạo tự động `PieceClickHitbox` chuẩn trục thế giới (`0.105m × 0.36m × 0.105m`) giúp click nhạy trên mọi loại Skin 3D cao (`gundam_unicorn`, `odin_transformer`, `arya`).
   - Tạo vòng sáng chọn quân dưới chân (`PieceSelectionRing`) và vòng sáng trạng thái (`PieceStatusRing`: Vàng cho Elite, Đỏ cho Nemesis).
   - Đồng bộ toàn bộ trạng thái qua Netcode (`SyncStateRpc`).
4. [`Assets/Scripts/Gameplay/ClickSelectAndMove.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Gameplay/ClickSelectAndMove.cs)
   - Cập nhật `FindClickedPiece(Ray ray)` ưu tiên bắt trúng quân hợp lệ của người chơi đang tới lượt + hỗ trợ kiểm tra khoảng cách tia Raycast tới trục thân nhân vật 3D.
   - Xử lý tiêu thụ từng slot hành động (`Dice1`, `Dice2`, `Bonus3`) cho cả chế độ Offline và Online.
5. [`Assets/Scripts/Gameplay/MovePathHighlighter.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Gameplay/MovePathHighlighter.cs)
   - Cập nhật đường vẽ preview cho Rolling Spawn từ trong chuồng, cộng thêm đường trượt `+3` nếu điểm đáp trùng Ô Tăng Tốc, và preview đi thẳng vào đường đích.

### 2.2. Quản Lý Bàn Cờ, Lượt & Xúc Xắc (`Assets/Scripts/Managers/`)
6. [`Assets/Scripts/Managers/BoardManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Managers/BoardManager.cs)
   - Khởi tạo trận Party Clash (`InitializePartyClashMatch`): đặt 2 quân mỗi người chơi ở `+0` và `+3`, vẽ vòng sáng Cyan cho 4 Ô Tăng Tốc (`7, 21, 35, 49`).
   - Kiểm tra luật bắt buộc xuất quân (`RequiresMandatorySpawn`), kiểm tra tính hợp lệ của từng viên xúc xắc (`IsPieceActionValid`, `HasAnyValidPieceForSlot`).
   - Quản lý mảng điểm `playerScores`, `playerHomeCounts`, `playerKickCounts`, xoay vòng sự kiện mỗi 3 vòng (`AdvanceRoundAndEvents`) và xếp hạng Tie-break (`GetSortedRankingOrder`).
7. [`Assets/Scripts/Managers/DiceManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Managers/DiceManager.cs)
   - Quản lý trạng thái từng viên (`dice1Used`, `dice2Used`, `bonus3Available`, `bonus3Used`, `selectedActionSlot`).
   - Cho phép chuyển đổi linh hoạt giữa `[Viên 1]`, `[Viên 2]`, `[Combo +3]` (`SelectActionSlot`), tự động chuyển sang viên còn lại sau khi đi xong viên đầu (`ConsumeSlotAfterMove`, `EvaluateRemainingActionsOrEndTurn`).
8. [`Assets/Scripts/Managers/TurnManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Managers/TurnManager.cs) & [`Assets/Scripts/Managers/TurnTimerManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Managers/TurnTimerManager.cs)
   - Quản lý đồng hồ trận 10 phút, Vòng Lượt Cuối (`isFinalRound`), đồng hồ suy nghĩ mỗi hành động (`RefillDecisionTimer`), và kích hoạt sự kiện toàn bàn.
9. [`Assets/Scripts/Managers/GameManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Managers/GameManager.cs)
   - Bật `Application.runInBackground = true` trong `Awake()` để không bị treo Coroutine khi cửa sổ game mất tiêu điểm.

### 2.3. Giao Diện Người Dùng (`Assets/Scripts/UI/`)
10. [`Assets/Scripts/UI/DiceRollPresentation.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/UI/DiceRollPresentation.cs)
    - Đổi popup lắc xúc xắc từ cộng tổng cũ sang **`"2 HÀNH ĐỘNG ĐỘC LẬP"`** và **`"Viên 1: [X]   |   Viên 2: [Y]"`**, rút ngắn thời gian chờ lắc để nhịp độ trận nhanh hơn.
11. [`Assets/Scripts/UI/GameplayUI.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/UI/GameplayUI.cs) & [`Assets/Scripts/UI/DiceButtonUI.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/UI/DiceButtonUI.cs)
    - Tự động sinh bằng code C# (không làm hỏng liên kết Inspector trong `GameScene.unity`):
      - Thanh 3 nút chọn hành động: **`[Viên 1: X]`**, **`[Viên 2: Y]`**, **`[Combo +3]`**.
      - Bảng **Scoreboard Thời Gian Thực** ở góc phải trên (hiển thị đồng hồ trận 10:00, Banner Sự Kiện hiện tại, Điểm số / Số lần về đích / Số lần đá / Tag `[TOP 1]` của từng người chơi, và dòng thông báo sự kiện vừa diễn ra).

### 2.4. Đồng Bộ Mạng (`Assets/Scripts/Network/`)
12. [`Assets/Scripts/Network/NetworkDiceManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Network/NetworkDiceManager.cs)
13. [`Assets/Scripts/Network/NetworkPieceMoveManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Network/NetworkPieceMoveManager.cs)
14. [`Assets/Scripts/Network/NetworkTurnManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Network/NetworkTurnManager.cs)
15. [`Assets/Scripts/Network/NetworkGameResultManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Network/NetworkGameResultManager.cs)
16. [`Assets/Scripts/Network/NetworkRoomControlManager.cs`](file:///d:/ProjectUnity/3DCoCaNgua/Assets/Scripts/Network/NetworkRoomControlManager.cs)
    - Đồng bộ các biến `NetworkVariable` cho trạng thái từng viên xúc xắc, gửi `TurnActionSlot` qua ServerRpc khi di chuyển quân, đồng bộ bảng điểm/sự kiện về tất cả Client qua `SyncPartyClashStateRpc`, và tính điểm xếp hạng cuối trận.

---

## 3. Những Công Việc Cần Làm Tiếp Theo (Khi Chuyển Lên PC)

Khi mở dự án trên PC mới, bạn có thể đưa file `PARTY_CLASH_HANDOVER.md` này cho AI Assistant và tiếp tục triển khai các hạng mục **Phase 2 & Phase 3** sau:

### 3.1. Tinh chỉnh Trải nghiệm Thị giác & 3D Board (Visual & UX Polish)
- [ ] **Hiệu ứng Floating Combat Text (+1đ, +3đ, +4đ, +6đ):**
  - Tạo chữ nổi 3D (`TextMeshPro` World Space bay lên và mờ dần) ngay tại vị trí quân cờ khi:
    - Qua mốc Checkpoint bước 28 (`+1đ ELITE!`)
    - Đá quân địch (`+1đ KICK!` hoặc `+3đ BOUNTY!`)
    - Về chuồng thu điểm (`+4đ HOME!` / `+6đ DEEP HOME!`)
- [ ] **Nâng cấp hiển thị trên bàn cờ 3D:**
  - Đánh dấu trực quan **Mốc Checkpoint (bước 28)** của từng màu hoặc hiển thị icon nhỏ trên 4 **Ô Tăng Tốc (`7, 21, 35, 49`)** rõ nét hơn vòng LineRenderer hiện tại.
  - Phân biệt màu sắc trực quan giữa **Ô Đích Nông (bậc 1–3: +4đ)** và **Ô Đích Sâu (bậc 4–6: +6đ)** trên đường về đích của mỗi màu.
- [ ] **Cải thiện bố cục HUD (`GameplayUI.cs`):**
  - Kiểm tra độ co giãn (`CanvasScaler`) trên các tỷ lệ màn hình khác nhau (16:9, 16:10, 21:9) để các bảng `TurnHudPopup`, `PartyClashActionSlots` và `PartyClashScoreboardPopup` không đè lên các nút điều khiển góc màn hình.

### 3.2. Kiểm thử Chuyên Sâu Chế Độ Multiplayer (Netcode for GameObjects)
- [ ] **Test thực tế Host + Client (2 – 4 người chơi):**
  - Kiểm tra luồng chọn `[Viên 1]` / `[Viên 2]` / `[Combo +3]` từ phía Client (không phải Host) qua `NetworkDiceManager` và `NetworkPieceMoveManager`.
  - Kiểm tra xử lý khi 1 người chơi thoát phòng giữa trận (`NetworkRoomControlManager.HandleOpponentLeftMatch`) xem bảng điểm và vòng lượt (`startingPlayerIndex`) có chuyển mượt mà không.
  - Kiểm tra màn hình tổng kết `GameOverUI` khi đồng hồ 10 phút kết thúc ở chế độ Online (`NetworkGameResultManager`).

### 3.3. Đồng bộ Backend Server (Nếu sử dụng `Backend/CoCaNgua.Server.csproj`)
- [ ] Thư mục `Backend/` hiện đang target `.NET 10.0` (`net10.0`). Nếu PC của bạn dùng `.NET 8.0` hoặc `.NET 9.0`, cần chỉnh `<TargetFramework>` trong `Backend/CoCaNgua.Server.csproj` hoặc cài SDK tương ứng, và cập nhật model lưu lịch sử kết quả trận đấu nếu Backend có lưu điểm số (`playerScores`, `playerKickCounts`, `playerHomeCounts`).

---

## 4. Hướng Dẫn Nhanh Cho AI Trên PC Mới
Khi mở dự án trên PC mới, bạn chỉ cần nhắn câu lệnh sau vào khung chat:
> *"Hãy đọc file `@[PARTY_CLASH_HANDOVER.md]` để nắm toàn bộ luật chơi Party Clash v0.2 và những gì đã làm ở Phase 1, sau đó giúp tôi tiếp tục triển khai các mục trong phần 3 (Những công việc cần làm tiếp theo)."*
