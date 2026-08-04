class Solution {
public:
    int maxProfit(vector<int>& prices) {
        int cheapest = prices[0];
        int best = 0;
        for (int price : prices) {
            cheapest = min(cheapest, price);
            best = max(best, price - cheapest);
        }
        return best;
    }
};
