import React, { useState, useEffect } from 'react';
import { AppBar, Toolbar, Typography, Snackbar, Button, Container, TextField, IconButton, Card, CardContent, Table, TableHead, TableRow, TableCell, TableBody, TableContainer } from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import DeleteIcon from '@mui/icons-material/Delete';

import passKeyService from './services/passkey-service';
import sessionStorageService from './services/session-storage-service';
import userService from './services/user-service';
import otpService from './services/otp-service';
import { SessionConstants } from './constants';
import {
  get,
} from '@github/webauthn-json/browser-ponyfill';

const headers = [
  { title: 'Id', value: 'id' },
  { title: 'Creation Time', value: 'createdAtUtc' },
  { title: 'Last Use Time', value: 'updatedAtUtc' },
  { title: 'Last Used on', value: 'lastUsedPlatformInfo' },
  { title: 'Actions', key: 'actions', sortable: false },
];

const App = () => {
  const [isLoggedIn, setIsLoggedIn] = useState(sessionStorageService.get(SessionConstants.TokenKey) != null);
  const [search, setSearch] = useState("");
  const [userName, setUserName] = useState("");
  const [otp, setOtp] = useState("");
  const [isOtpSent, setIsOtpSent] = useState(false);
  const [bearerToken, setBearerToken] = useState("");
  const [snackbarMessage, setSnackbarMessage] = useState("");
  const [snackbarColor, setSnackbarColor] = useState("success");
  const [user, setUser] = useState({ credentials: [] });

  useEffect(() => {
    if (isLoggedIn) {
      refreshUser();
    }
  }, [isLoggedIn]);

  const refreshUser = async () => {
    const loggedInUser = await userService.getUser();
    setUser(loggedInUser);
  };

  const validateInputs = () => {
    if (!userName) {
      setSnackbarColor("error");
      setSnackbarMessage("Username can't be empty!");
      return false;
    }
    return true;
  };

  const logout = () => {
    sessionStorageService.clear(SessionConstants.TokenKey);
    setIsOtpSent(false);
    setIsLoggedIn(false);
  };

  const handleError = (error) => {
    console.log("SEGUNDA CHAMADA", error);
    setSnackbarColor("error");
    setSnackbarMessage(error.message);
  };

  const handleLoginSuccess = () => {
    setSnackbarColor("success");
    setSnackbarMessage(`Successfully logged in as ${userName}`);
  };

  const createLoginPassKey = async () => {
    if (!validateInputs()) return;

    try {
      const credentialOptions = await passKeyService.createCredentialOptions(userName);
      const result = await passKeyService.createCredential(credentialOptions.userId, credentialOptions.options);
      const { credentialMakeResult, token } = result;

      if (credentialMakeResult.status === 'ok') {
        sessionStorageService.set(SessionConstants.TokenKey, token, SessionConstants.TokenExpiryTime);
        await refreshUser();
        setIsLoggedIn(true);
        handleLoginSuccess();
      }
    } catch (error) {
      handleError(error);
    }
  };

  const registerAdditionalPassKey = async () => {
    try {
      const credentialOptions = await passKeyService.createCredentialOptionsForCurrentUser();
      await passKeyService.createCredential(credentialOptions.userId, credentialOptions.options);
      await refreshUser();
    } catch (error) {
      handleError(error);
    }
  };


  const handlePasskeyLogin = async () => {
    try {
      const credentialOptions = await passKeyService.createCredentialOptions();
      const result = await passKeyService.createCredential(credentialOptions.userId, credentialOptions.options);
      const { credentialMakeResult, token } = result;

      if (credentialMakeResult.status === 'ok') {
        sessionStorageService.set(SessionConstants.TokenKey, token, SessionConstants.TokenExpiryTime);
        await refreshUser();
        setIsLoggedIn(true);
        handleLoginSuccess();
      }
    } catch (error) {
      handleError(error);
    }
  };

  const verifyOtp = async () => {
    if (!otp) {
      setSnackbarColor("error");
      setSnackbarMessage("OTP can't be empty!");
      return;
    }

    try {
      const token = await otpService.verifyOtp(otp, bearerToken);
      setBearerToken(token); // Update the bearer token with the new one
      sessionStorageService.set(SessionConstants.TokenKey, token, SessionConstants.TokenExpiryTime);
      
      //var hasCredential = await passKeyService.checkCredential();

        await loginWithPassKeys();
      
      //await handlePasskeyLogin(); 
    } catch (error) {
      handleError(error);
    }
  };

  const sendOtp = async () => {
    if (!validateInputs()) return;

    try {
      const { token } = await otpService.generateOtp(userName);
      setBearerToken(token);
      setIsOtpSent(true);
      setSnackbarColor("success");
      setSnackbarMessage("OTP sent successfully!");
    } catch (error) {
      handleError(error);
    }
  };

  const loginWithPassKeys = async () => {
    
    console.log("PRIMEIRA CHAMADA");
    try {
      const assertionOptions = await passKeyService.createAssertionOptions();

      console.log("RESPOSTA DO CREATE",assertionOptions );

      const result = await passKeyService.verifyAssertion(assertionOptions.options);
      const { assertionVerificationResult, token } = result;

      if (assertionVerificationResult.status === 'ok') {
        sessionStorageService.set(SessionConstants.TokenKey, token, SessionConstants.TokenExpiryTime);
        await refreshUser();
        setIsLoggedIn(true);
        handleLoginSuccess();
      }
    } catch (error) {
      handleError(error);
    }
  };

  const revokePassKey = async (credentialId) => {
    try {
      await passKeyService.revokeCredential(credentialId);
      await refreshUser();
    } catch (error) {
      handleError(error);
    }
  };

  return (
    <div>
      <AppBar position="static" color="secondary">
        <Toolbar>
          <Typography variant="h6">Pass Key Demo</Typography>
        </Toolbar>
      </AppBar>
      <Container>
        <Snackbar
          open={Boolean(snackbarMessage)}
          autoHideDuration={6000}
          onClose={() => setSnackbarMessage('')}
          message={snackbarMessage}
          action={
            <IconButton size="small" color="inherit" onClick={() => setSnackbarMessage('')}>
              <CloseIcon fontSize="small" />
            </IconButton>
          }
        />
        {!isLoggedIn ? (
          !isOtpSent ? (
            <div align="center">
              <TextField
                variant="outlined"
                label="Username"
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                fullWidth
              />
              <Button variant="contained" color="primary" onClick={loginWithPassKeys}>
                LOGIN
              </Button>
            </div>
          ) : (
            <>
              <TextField
                variant="outlined"
                label="OTP"
                value={otp}
                onChange={(e) => setOtp(e.target.value)}
                fullWidth
              />
              <Button variant="contained" color="primary" onClick={verifyOtp}>
                Verify OTP
              </Button>
            </>
          )
        ) : (
          <>
            <Typography padding={6} variant="h4" align="center">Olá, {user.userName}!</Typography>
            <Card>
              <CardContent>
                
                <TableContainer>
                  <Table>
                    <TableHead>
                      <TableRow>
                        {headers.map((header) => (
                          <TableCell key={header.value}>{header.title}</TableCell>
                        ))}
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {user.credentials.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell>{item.id}</TableCell>
                          <TableCell>{item.createdAtUtc}</TableCell>
                          <TableCell>{item.updatedAtUtc}</TableCell>
                          <TableCell>{item.lastUsedPlatformInfo}</TableCell>
                          <TableCell>
                            <IconButton size="small" onClick={() => revokePassKey(item.id)}>
                              <DeleteIcon fontSize="small" />
                            </IconButton>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
                <Button variant="contained" color="secondary" onClick={logout}>
                  Logout
                </Button>
              </CardContent>
            </Card>
          </>
        )}
      </Container>
    </div>
  );
};

export default App;
