using System;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Interfaces
{
    // Dữ liệu phục vụ màn hình menu chính: số dư tiền tệ (coins).
    public interface IMenuDataService
    {
        // ↓ LẤY VỀ: toàn bộ số dư virtual currency của người chơi (mã tiền tệ -> số dư).
        // Binder tự chọn mã cần dùng (ví dụ "GC").
        void GetVirtualCurrency(Action<Dictionary<string, int>> onResult, Action<AuthError> onError);

        // ↓ LẤY VỀ: danh sách ItemId người chơi ĐANG SỞ HỮU (từ PlayFab inventory). Dùng để đánh dấu "đã mua".
        void GetOwnedItems(Action<List<string>> onResult, Action<AuthError> onError);

        // MUA vật phẩm: trừ virtual currency + cấp item qua PlayFab Catalog/Store (server tự kiểm giá).
        // itemId khớp ItemId trong PlayFab Catalog; currencyCode ví dụ "GC"; price = giá theo VC.
        void PurchaseItem(string itemId, string currencyCode, int price, Action onSuccess, Action<AuthError> onError);
    }
}
