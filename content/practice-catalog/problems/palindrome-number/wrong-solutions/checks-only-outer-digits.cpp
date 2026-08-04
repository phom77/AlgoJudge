class Solution {
public:
    bool isPalindrome(int x) {
        if (x < 0) return false;
        if (x < 10) return true;
        int last = x % 10;
        while (x >= 10) x /= 10;
        return x == last;
    }
};
