namespace GameServerApi.Models;

// 商品マスタは学習用にコード内で固定し、価格をクライアントから受け取らない。
public record ShopItem(string Code, string Name, int PriceGold);
