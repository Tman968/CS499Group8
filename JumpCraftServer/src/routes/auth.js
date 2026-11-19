import { Router } from 'express';
import argon2 from 'argon2';
import { rateLimit } from 'express-rate-limit';
import { pool } from '../lib/database.js';

const router = Router();

const registerLimiter = rateLimit({
    windowMs: 15 * 60 * 1000,
    limit: 10,
    standardHeaders: 'draft-8',
    legacyHeaders: false
});

router.post('/register', registerLimiter, async (req, res) => {
    const { username, password } = req.body ?? {};

    // Validate submitted data.
    if (typeof username !== 'string' ||
        typeof password !== 'string') {
        return res.status(400).json({
            error: 'Username and password are required'
        });
    }

    const cleanUsername = username.trim();

    if (!/^[A-Za-z0-9_]{3,20}$/.test(cleanUsername)) {
        return res.status(400).json({
            error: 'Username must be 3-20 characters (letters, numbers, underscores)'
        });
    }

    if (password.length < 12 || password.length > 128) {
        return res.status(400).json({
            error: 'Password must be between 12 and 128 characters'
        });
    }

    try {
        // Hash the password before storing it.
        const passwordHash = await argon2.hash(password, {
            type: argon2.argon2id
        });

        // Save the account using a parameterized SQL query.
        const result = await pool.query(
            `INSERT INTO jumpcraft.player_accounts
             (username, password_hash)
             VALUES ($1, $2)
             RETURNING id, username, created_at`,
            [cleanUsername, passwordHash]
        );

        return res.status(201).json({
            message: 'Account created successfully!',
            player: result.rows[0]
        });

    } catch (error) {
        if (error.code === '23505') {
            return res.status(409).json({
                error: 'Username already exists'
            });
        }

        console.error('Registration error:', error.message);

        return res.status(500).json({
            error: 'Unable to create account'
        });
    }
});


const loginLimiter = rateLimit({
    windowMs: 15 * 60 * 1000,
    limit: 10,
    standardHeaders: 'draft-8',
    legacyHeaders: false
});

// LOGIN
router.post('/login', loginLimiter, async (req, res) => {
    const { username, password } = req.body ?? {};

    if (
        typeof username !== 'string' ||
        typeof password !== 'string' ||
        !/^[A-Za-z0-9_]{3,20}$/.test(username.trim()) ||
        password.length > 128
    ) {
        return res.status(400).json({
            error: 'Invalid login input'
        });
    }

    try {
        const result = await pool.query(
            `SELECT id, username, password_hash
             FROM jumpcraft.player_accounts
             WHERE LOWER(username) = LOWER($1)`,
            [username.trim()]
        );

        const player = result.rows[0];

        const validPassword = player
            ? await argon2.verify(player.password_hash, password)
            : false;

        if (!validPassword) {
            return res.status(401).json({
                error: 'Invalid username or password'
            });
        }

        // Generate a fresh session after login.
        req.session.regenerate((error) => {
            if (error) {
                return res.status(500).json({
                    error: 'Unable to create session'
                });
            }

            req.session.playerId = player.id;

            req.session.save((error) => {
                if (error) {
                    return res.status(500).json({
                        error: 'Unable to save session'
                    });
                }

                return res.json({
                    message: 'Login successful!',
                    player: {
                        id: player.id,
                        username: player.username
                    }
                });
            });
        });

    } catch (error) {
        console.error('Login error:', error.message);

        return res.status(500).json({
            error: 'Unable to log in'
        });
    }
});

// GET CURRENT PLAYER
router.get('/me', async (req, res) => {
    if (!req.session.playerId) {
        return res.status(401).json({
            error: 'Not authenticated'
        });
    }

    try {
        const result = await pool.query(
            `SELECT id, username, created_at
             FROM jumpcraft.player_accounts
             WHERE id = $1`,
            [req.session.playerId]
        );

        if (result.rows.length === 0) {
            return res.status(401).json({
                error: 'Account not found'
            });
        }

        return res.json({
            player: result.rows[0]
        });

    } catch (error) {
        console.error('Profile error:', error.message);

        return res.status(500).json({
            error: 'Unable to retrieve player'
        });
    }
});

// LOGOUT
router.post('/logout', (req, res) => {
    req.session.destroy((error) => {
        if (error) {
            return res.status(500).json({
                error: 'Unable to log out'
            });
        }

        res.clearCookie('jumpcraft.sid', {
            path: '/'
        });

        return res.json({
            message: 'Logged out successfully!'
        });
    });
});

export default router;