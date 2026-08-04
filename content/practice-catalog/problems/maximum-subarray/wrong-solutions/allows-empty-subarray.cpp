class Solution {
public:
    int maxSubArray(vector<int>& nums) {
        int current = 0;
        int best = 0;
        for (int value : nums) {
            current = max(0, current + value);
            best = max(best, current);
        }
        return best;
    }
};
