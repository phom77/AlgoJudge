class Solution {
public:
    vector<int> rotateRight(vector<int> nums, int k) {
        k %= static_cast<int>(nums.size());
        reverse(nums.begin(), nums.end());
        reverse(nums.begin(), nums.begin() + k);
        reverse(nums.begin() + k, nums.end());
        return nums;
    }
};
